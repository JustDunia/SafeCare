using System.Text;

namespace SafeCare.Tests.Unit;

/// <summary>
/// Guards against source files being saved in a single-byte codepage. Windows-1250 bytes are
/// valid on disk but decode as replacement characters when the compiler reads them as UTF-8,
/// which once silently broke every Polish label on the public form.
/// </summary>
public class SourceEncodingTests
{
    private static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SafeCare.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!;
    }

    public static TheoryData<string> SourceFiles()
    {
        var root = RepositoryRoot();
        var data = new TheoryData<string>();

        foreach (var file in root.EnumerateFiles("*.*", SearchOption.AllDirectories))
        {
            if (file.Extension is not (".cs" or ".razor"))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root.FullName, file.FullName);

            if (relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                || relative.Contains($"{Path.DirectorySeparatorChar}.claude{Path.DirectorySeparatorChar}")
                || relative.Contains($"{Path.DirectorySeparatorChar}.superpowers{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            data.Add(relative);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void SourceFileIsValidUtf8(string relativePath)
    {
        var fullPath = Path.Combine(RepositoryRoot().FullName, relativePath);
        var bytes = File.ReadAllBytes(fullPath);

        // Throw-on-invalid decoders turn a mis-encoded byte sequence into an exception
        // instead of silently substituting U+FFFD.
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        var exception = Record.Exception(() => strictUtf8.GetString(bytes));

        Assert.Null(exception);
    }

    [Theory]
    [MemberData(nameof(SourceFiles))]
    public void SourceFileContainsNoReplacementCharacter(string relativePath)
    {
        var fullPath = Path.Combine(RepositoryRoot().FullName, relativePath);

        // Written as an escape rather than the literal glyph: the literal glyph is itself the
        // U+FFFD byte sequence, so this very file would always fail its own check otherwise.
        Assert.DoesNotContain((char)0xFFFD, File.ReadAllText(fullPath));
    }
}
