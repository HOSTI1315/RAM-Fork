namespace RAM.Core;

public readonly struct Result<T, TError>
{
    public bool IsOk { get; }
    private readonly T? _value;
    private readonly TError? _error;

    private Result(bool ok, T? value, TError? error)
    {
        IsOk = ok;
        _value = value;
        _error = error;
    }

    public T Value => IsOk
        ? _value!
        : throw new InvalidOperationException("Result is Err; check IsOk before accessing Value.");

    public TError Error => !IsOk
        ? _error!
        : throw new InvalidOperationException("Result is Ok; check IsOk before accessing Error.");

    public static Result<T, TError> Ok(T value) => new(true, value, default);
    public static Result<T, TError> Err(TError error) => new(false, default, error);

    public TOut Match<TOut>(Func<T, TOut> ok, Func<TError, TOut> err) =>
        IsOk ? ok(_value!) : err(_error!);

    public bool TryGetValue(out T value, out TError error)
    {
        value = _value!;
        error = _error!;
        return IsOk;
    }
}

public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
