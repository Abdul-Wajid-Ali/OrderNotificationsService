namespace OrderNotificationsService.Features.Common
{
    // Implements HandlerResult behavior for the order notifications service.
    public sealed class HandlerResult
    {
        private HandlerResult(bool isSuccess, HandlerErrorCode errorCode)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
        }

        public bool IsSuccess { get; }

        public HandlerErrorCode ErrorCode { get; }

        public static HandlerResult Success() => new(true, HandlerErrorCode.None);

        public static HandlerResult Failure(HandlerErrorCode errorCode) => new(false, errorCode);
    }
}