namespace EduManageLms.Api.Common;
public sealed record ApiError(string? Field, string Message);
public sealed record ApiResponse<T>(bool Success,string Message,T? Data,IReadOnlyCollection<ApiError>? Errors,DateTime Timestamp)
{
 public static ApiResponse<T> Ok(T data,string message="Lấy dữ liệu thành công")=>new(true,message,data,null,DateTime.UtcNow);
 public static ApiResponse<T> Fail(string message,IReadOnlyCollection<ApiError>? errors=null)=>new(false,message,default,errors,DateTime.UtcNow);
}
public sealed record PagedResult<T>(IReadOnlyCollection<T> Items,int PageNumber,int PageSize,long TotalItems,int TotalPages,bool HasPreviousPage,bool HasNextPage)
{
 public static PagedResult<T> Create(IReadOnlyCollection<T> items,int page,int size,long total)=>new(items,page,size,total,(int)Math.Ceiling(total/(double)size),page>1,page*size<total);
}
