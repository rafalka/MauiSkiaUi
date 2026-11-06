using System.Collections.Frozen;

namespace MauiSkiaUi.Markdown.Math;

/// <summary>
/// Decides whether the math engine may see a formula, without parsing it. The engine's parser, typesetter and drawing recurse, and a stack overflow cannot be
/// caught: on a 512 KB stack a short formula of 500 <c>\frac</c> ends the process. So everything that can nest shares one budget, also what nests without braces
/// (<c>\sqrt\sqrt</c>, <c>\frac1\frac1</c>, <c>^^</c>); a formula past it shows its source.
/// </summary>
internal static class SkUiMathGuard
{
    /// <summary>Longest formula.</summary>
    internal const int MaximumLength = 4_096;

    /// <summary>
    /// How deep a formula can get: open braces, plus commands that can hold something, plus scripts that hold more than one symbol. Commands and such scripts count
    /// for the whole formula, because they can nest without braces; unknown commands count too, so a new one in the engine cannot get past.
    /// </summary>
    internal const int MaximumNesting = 48;

    /// <summary>Most <c>\left</c> open at once: a group typesets what it holds more than once, so the time grows exponentially with this and little else stops it.</summary>
    internal const int MaximumLeftDepth = 8;

    // Symbols and operators: they hold nothing, and a long formula has many.
    private static readonly FrozenSet<string> LeafCommands = FrozenSet.ToFrozenSet(
    [
        "alpha", "beta", "gamma", "delta", "epsilon", "varepsilon", "zeta", "eta", "theta", "vartheta", "iota", "kappa", "lambda", "mu", "nu", "xi", "omicron", "pi", "varpi", "rho", "varrho",
        "sigma", "varsigma", "tau", "upsilon", "phi", "varphi", "chi", "psi", "omega", "Gamma", "Delta", "Theta", "Lambda", "Xi", "Pi", "Sigma", "Upsilon", "Phi", "Psi", "Omega",
        "sum", "prod", "coprod", "int", "iint", "iiint", "oint", "bigcup", "bigcap", "bigvee", "bigwedge", "bigoplus", "bigotimes",
        "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "coth", "exp", "log", "ln", "lg", "lim", "limsup", "liminf", "max", "min", "sup", "inf",
        "det", "dim", "gcd", "ker", "deg", "arg", "hom", "Pr",
        "cdot", "times", "div", "pm", "mp", "ast", "star", "circ", "bullet", "oplus", "ominus", "otimes", "oslash", "wedge", "vee", "cap", "cup", "setminus", "dagger", "ddagger",
        "le", "leq", "ge", "geq", "ne", "neq", "equiv", "approx", "sim", "simeq", "cong", "propto", "ll", "gg", "in", "notin", "ni", "subset", "subseteq", "supset", "supseteq", "perp", "parallel", "mid",
        "to", "gets", "rightarrow", "leftarrow", "leftrightarrow", "Rightarrow", "Leftarrow", "Leftrightarrow", "longrightarrow", "longleftarrow", "mapsto", "uparrow", "downarrow", "implies", "iff",
        "infty", "partial", "nabla", "forall", "exists", "nexists", "emptyset", "varnothing", "aleph", "hbar", "ell", "Re", "Im", "angle", "triangle", "neg", "lnot", "prime",
        "ldots", "cdots", "vdots", "ddots", "dots", "quad", "qquad", "lbrace", "rbrace", "langle", "rangle", "lfloor", "rfloor", "lceil", "rceil", "vert", "Vert", "backslash",
        "right", "end", "nonumber", "displaystyle", "textstyle", "limits", "nolimits"
    ], StringComparer.Ordinal);

    /// <summary>Whether <paramref name="latex"/> is short and shallow enough for the engine.</summary>
    internal static bool IsSafe(string latex)
    {
        if (latex.Length > MaximumLength)
            return false;
        var braces = 0;
        var permanent = 0; // commands and scripts that hold something: they can nest without braces
        var lefts = 0;
        for (var index = 0; index < latex.Length; index++)
        {
            var character = latex[index];
            switch (character)
            {
                case '{':
                    braces++;
                    break;
                case '}':
                    braces = System.Math.Max(0, braces - 1);
                    break;
                case '\\':
                    var start = index + 1;
                    var end = start;
                    while (end < latex.Length && char.IsAsciiLetter(latex[end]))
                        end++;
                    if (end == start)
                    {
                        index++; // an escaped character (\{, \}, \\) is a symbol: it opens and closes nothing
                        break;
                    }
                    index = end - 1;
                    var command = latex[start..end];
                    if (command == "left" && ++lefts > MaximumLeftDepth)
                        return false;
                    if (command == "right")
                        lefts = System.Math.Max(0, lefts - 1);
                    if (!LeafCommands.Contains(command))
                        permanent++;
                    break;
                case '%':
                    return false; // a comment: the engine skips the rest of the line, so a closing brace in it would close nothing there and still count here
                case '^' or '_':
                    if (!HoldsOneSymbol(latex, index + 1))
                        permanent++;
                    break;
                default:
                    if (char.IsControl(character) && !char.IsWhiteSpace(character))
                        return false; // the engine skips these, so they would hide what is next to them from this count
                    break;
            }
            if (braces + permanent > MaximumNesting)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Whether a script whose content starts at <paramref name="index"/> holds one symbol, and so cannot nest: <c>x^2</c>, <c>x^\alpha</c>. A braced script counts
    /// through its brace, and only while it is open: a long formula has many <c>x_{10}</c>. What this does not know counts, so nothing unknown can nest for free.
    /// </summary>
    private static bool HoldsOneSymbol(string latex, int index)
    {
        while (index < latex.Length && char.IsWhiteSpace(latex[index]))
            index++;
        if (index >= latex.Length)
            return true;
        var character = latex[index];
        if (character == '{' || char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '*' or '/' or '=' or '<' or '>' or '(' or ')' or '[' or ']' or '|' or ',' or '.' or ':' or ';' or '!' or '?' or '\'')
            return true;
        if (character != '\\')
            return char.IsLetter(character);
        var end = index + 1;
        while (end < latex.Length && char.IsAsciiLetter(latex[end]))
            end++;
        return end > index + 1 && LeafCommands.Contains(latex[(index + 1)..end]);
    }
}
