namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's <c>NavigationHistory</c>: the commits selected one after another in the grid, for "Navigate backward" and
///  "Navigate forward".
/// </summary>
public sealed class NavigationHistory
{
    private readonly Stack<string> _previous = new();
    private readonly Stack<string> _next = new();

    public bool CanNavigateBackward => _previous.Count > 1;

    public bool CanNavigateForward => _next.Count != 0;

    /// <summary>
    ///  Records a selected commit; selecting a new one forgets the commits navigated back from.
    /// </summary>
    public void Push(string hash)
    {
        if (_previous.Count == 0 || _previous.Peek() != hash)
        {
            _previous.Push(hash);
            _next.Clear();
        }
    }

    /// <summary>
    ///  The commit selected before the current one, or null when there is none.
    /// </summary>
    public string? NavigateBackward()
    {
        if (!CanNavigateBackward)
        {
            return null;
        }

        _next.Push(_previous.Pop());
        return _previous.Peek();
    }

    /// <summary>
    ///  The commit navigated back from last, or null when there is none.
    /// </summary>
    public string? NavigateForward()
    {
        if (!CanNavigateForward)
        {
            return null;
        }

        string next = _next.Pop();
        _previous.Push(next);
        return next;
    }

    public void Clear()
    {
        _previous.Clear();
        _next.Clear();
    }
}
