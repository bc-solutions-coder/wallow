namespace Wallow.Shared.Kernel.Results;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// Use instead of throwing exceptions for expected failure cases.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("Success result cannot have an error");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("Failure result must have an error");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default!, false, error);

    /// <summary>
    /// Creates a result based on a condition.
    /// </summary>
    public static Result Create(bool condition, Error error) =>
        condition ? Success() : Failure(error);
}

/// <summary>
/// Represents the result of an operation that returns a value.
/// </summary>
public class Result<TValue> : Result
{
    private readonly TValue _value;

    public TValue Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Cannot access value of a failed result");

    protected internal Result(TValue value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>
    /// Maps the value to a new type if successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper) =>
        IsSuccess ? Result.Success(mapper(_value)) : Result.Failure<TNew>(Error);

    /// <summary>
    /// Chains another operation if successful.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<TValue, Result<TNew>> binder) =>
        IsSuccess ? binder(_value) : Result.Failure<TNew>(Error);
}
