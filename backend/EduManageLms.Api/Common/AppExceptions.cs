namespace EduManageLms.Api.Common;
public class AppException(string message,int statusCode=400):Exception(message){public int StatusCode{get;}=statusCode;}
public sealed class ForbiddenException(string message="Bạn không có quyền truy cập tài nguyên này"):AppException(message,403);
public sealed class NotFoundException(string message="Không tìm thấy tài nguyên"):AppException(message,404);
public sealed class ConflictException(string message):AppException(message,409);
