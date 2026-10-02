namespace Snail.MCP.Blender.Application.Discovery;

/// <summary>The words a piece of text is matched by: letters and digits, split on everything else, compared without case.</summary>
/// <remarks>Splitting by hand rather than by a regular expression keeps <c>bpy.ops.mesh.bevel</c>, <c>blender_mesh_bevel</c> and
/// "bevel the mesh" reducible to the same words, which is the whole trick discovery rests on.</remarks>
internal static class Vocabulary
{
    /// <summary>Words that carry no intent: the prefix every tool shares, and the glue of an English sentence.</summary>
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "blender", "bpy", "ops", "the", "a", "an", "and", "or", "of", "for", "to", "in", "on", "with", "how", "do", "does", "did",
        "i", "it", "its", "is", "are", "be", "that", "this", "my", "from", "into", "at", "by", "as", "can", "want", "need", "please", "some",
    };

    /// <summary>Every distinct word of the text, without the noise.</summary>
    public static HashSet<string> Of(string? text)
    {
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
        {
            return words;
        }

        var start = -1;

        for (var index = 0; index <= text.Length; index++)
        {
            if (index < text.Length && char.IsLetterOrDigit(text[index]))
            {
                start = start < 0 ? index : start;

                continue;
            }

            if (start >= 0)
            {
                Keep(words, text[start..index]);
                start = -1;
            }
        }

        return words;
    }

    private static void Keep(HashSet<string> words, string word)
    {
        if (word.Length > 1 && !Noise.Contains(word))
        {
            words.Add(word);
        }
    }
}
