using System.Text.Json;
using EduManageLms.Api.Common;
using MongoDB.Driver;

namespace EduManageLms.Api.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> log,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var (status, message) = MapException(exception);
            if (status >= StatusCodes.Status500InternalServerError)
                log.LogError(exception, "Unhandled exception");

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            if (status == StatusCodes.Status500InternalServerError
                && !env.IsDevelopment())
                message = "Đã xảy ra lỗi hệ thống";

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(
                    ApiResponse<object>.Fail(message),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));
        }
    }

    internal static (int Status, string Message) MapException(
        Exception exception)
    {
        if (exception is AppException appException)
            return (appException.StatusCode, appException.Message);

        if (exception is MongoWriteException validationWrite
                && validationWrite.WriteError.Code == 121
            || exception is MongoCommandException validationCommand
                && validationCommand.Code == 121)
        {
            return (
                StatusCodes.Status400BadRequest,
                "Dữ liệu chưa đúng cấu trúc bắt buộc. Vui lòng kiểm tra các trường đã nhập.");
        }

        if (exception is MongoWriteException writeException
                && writeException.WriteError.Category
                    == ServerErrorCategory.DuplicateKey
            || exception is MongoCommandException commandException
                && commandException.Code == 11000)
        {
            return (
                StatusCodes.Status409Conflict,
                "Dữ liệu bị trùng với một bản ghi đang hoạt động");
        }

        return (
            StatusCodes.Status500InternalServerError,
            exception.Message);
    }
}
