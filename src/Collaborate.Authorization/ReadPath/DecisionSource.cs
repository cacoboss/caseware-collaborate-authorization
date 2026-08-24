namespace Collaborate.Authorization.ReadPath;

/// <summary>Where the tree came from. Unavailable means nothing was read at all.</summary>
public enum DecisionSource
{
    Cache,
    Database,
    Unavailable
}
