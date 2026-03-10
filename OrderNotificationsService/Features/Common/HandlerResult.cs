namespace OrderNotificationsService.Features.Common
{
    // Represents the outcome of a handler operation with success state and error code
    public sealed class HandlerResult
    {
        private HandlerResult(bool isSuccess, HandlerErrorCode errorCode)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
        }

        // Indicates whether the handler executed successfully
        public bool IsSuccess { get; }

        // Error code describing the failure reason when the operation is unsuccessful
        public HandlerErrorCode ErrorCode { get; }

        // Creates a successful handler result
        public static HandlerResult Success() => new(true, HandlerErrorCode.None);

        // Creates a failed handler result with a specific error code
        public static HandlerResult Failure(HandlerErrorCode errorCode) => new(false, errorCode);
    }
}