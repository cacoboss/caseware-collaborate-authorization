namespace Collaborate.Authorization.Resolution;

/// <summary>The outcome of one authorization question, and why it came out that way.</summary>
public sealed record Decision(bool Allowed, DecidingRule DecidingRule)
{
    public static Decision Allow(DecidingRule rule) => new(true, rule);
    public static Decision Deny(DecidingRule rule) => new(false, rule);
}
