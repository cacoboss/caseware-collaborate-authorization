namespace Collaborate.Authorization.Resolution;

/// <summary>What one permission plane says about a question. Absent is not a deny.</summary>
public enum RuleOutcome
{
    Absent,
    Allow,
    Deny
}
