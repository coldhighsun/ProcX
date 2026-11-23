namespace ProcX.App.ViewModels;

/// <summary>
/// How a row is presented in the process tree, which determines its expander glyph.
/// </summary>
public enum TreeNodeKind
{
    /// <summary>
    /// The list is not in tree mode; no expander and no indentation.
    /// </summary>
    None,

    /// <summary>
    /// A process without children; the expander slot is reserved for alignment but empty.
    /// </summary>
    Leaf,

    /// <summary>
    /// A process whose children are hidden.
    /// </summary>
    Collapsed,

    /// <summary>
    /// A process whose children are shown.
    /// </summary>
    Expanded,
}
