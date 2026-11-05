using System.Collections.Frozen;

namespace MauiSkiaUi.Markdown.Math;

/// <summary>
/// Decides whether the math engine may see a formula, without parsing it. The engine's parser, typesetter and drawing recurse, and a stack overflow cannot be
/// caught: on a 512 KB stack a short formula of 500 <c>\frac</c> ends the process. Everything that can nest is counted, also what nests without braces
/// (<c>\sqrt\sqrt</c>, <c>\frac1\frac1</c>); a formula past a limit shows its source. The limits stay well inside what passed on that stack (100 levels).
/// </summary>
internal static class SkUiMathGuard
{
    /// <summary>Longest formula.</summary>
    internal const int MaximumLength = 4_096;

    /// <summary>Deepest <c>{</c>.</summary>
    internal const int MaximumBraceDepth = 32;

    /// <summary>Most commands that can hold something, in a formula: unknown ones count, so a new one in the engine cannot get past.</summary>
    internal const int MaximumNestingCommands = 48;

    /// <summary>Most of <c>^</c> and <c>_</c> in a row.</summary>
    private const int MaximumScriptRun = 8;

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
        var depth = 0;
        var commands = 0;
        var scripts = 0;
        for (var index = 0; index < latex.Length; index++)
        {
            var character = latex[index];
            switch (character)
            {
                case '{':
                    if (++depth > MaximumBraceDepth)
                        return false;
                    break;
                case '}':
                    depth = System.Math.Max(0, depth - 1);
                    break;
                case '\\':
                    var start = index + 1;
                    var end = start;
                    while (end < latex.Length && char.IsAsciiLetter(latex[end]))
                        end++;
                    index = System.Math.Max(index, end - 1);
                    if (end > start && !LeafCommands.Contains(latex[start..end]) && ++commands > MaximumNestingCommands)
                        return false;
                    break;
            }
            if (character is '^' or '_')
            {
                if (++scripts > MaximumScriptRun)
                    return false;
            }
            else if (!char.IsWhiteSpace(character))
                scripts = 0;
        }
        return true;
    }
}
