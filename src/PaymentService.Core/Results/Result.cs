namespace PaymentService.Core.Results;

public sealed class Result<T>
{
    private Result(T value) { Value = value; IsSuccess = true; }
    private Result(Error error) { Error = error; IsSuccess = false; Value = default!; }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; }
    public Error? Error { get; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}
