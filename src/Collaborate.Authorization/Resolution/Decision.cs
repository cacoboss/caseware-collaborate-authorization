namespace Collaborate.Authorization.Resolution;

public sealed record Decision(bool Allowed, DecidingRule DecidingRule)
{
    public static Decision Allow(DecidingRule rule) => new(true, rule);
    public static Decision Deny(DecidingRule rule) => new(false, rule);
}
