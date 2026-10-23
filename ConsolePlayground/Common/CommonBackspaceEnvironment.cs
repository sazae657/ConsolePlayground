using System.Text.RegularExpressions;

namespace ConsolePlayground.Cmd.Common;

/// <summary>
/// 共通
/// </summary>
internal static partial class CommonBackspaceEnvironment
{
    /// <summary>
    /// ヴァー
    /// </summary>
    public const char Separator = '\b';

    /// <summary>
    /// 改行置き換えコード
    /// </summary>
    const string Spacement = "\u007F";

    /// <summary>
    /// 世界標準の改行(Macintoshは知らん、滅べ)
    /// </summary>
    public const string LineBreak = "\r\n";

    /// <summary>
    /// 改行を引っかける(Macintoshは知らん、滅べ)
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex NL();

    /// <summary>
    ///エスケープ(Macintoshは知らん、滅べ)
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string Escape(this string input)
    {
        return NL().Replace(input, Spacement);
    }

    /// <summary>
    /// エスケープ解除(Macintoshは知らん、滅べ)
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string Unescape(this string input)
    {
        return input.Replace(Spacement, LineBreak);
    }
}
