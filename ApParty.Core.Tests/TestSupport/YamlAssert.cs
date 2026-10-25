using System.Text;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ApParty.Core.Tests.TestSupport;

internal static class YamlAssert
{
    public static void Equivalent(string expected, string actual, params string[] ignoredTopLevelKeys) =>
        Assert.Equal(Canonical(Root(expected), ignoredTopLevelKeys), Canonical(Root(actual), ignoredTopLevelKeys));

    public static void OptionEquivalent(string expectedOptionYaml, string actualDocument, string gameName, string option)
    {
        var gameSection = (YamlMappingNode)((YamlMappingNode)Root(actualDocument))[gameName];
        Assert.True(gameSection.Children.ContainsKey(option), $"Option '{option}' missing from '{gameName}' section.");
        Assert.Equal(Canonical(Root(expectedOptionYaml)), Canonical(gameSection[option]));
    }

    private static YamlNode Root(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents[0].RootNode;
    }

    private static string Canonical(YamlNode node, params string[] ignoredTopLevelKeys)
    {
        if (node is YamlMappingNode mapping && ignoredTopLevelKeys.Length > 0)
        {
            node = new YamlMappingNode(mapping.Children
                .Where(kv => !ignoredTopLevelKeys.Contains(((YamlScalarNode)kv.Key).Value)));
        }

        var sb = new StringBuilder();
        Write(node, sb, indent: 0);
        return sb.ToString();
    }

    private static void Write(YamlNode node, StringBuilder sb, int indent)
    {
        var pad = new string(' ', indent);
        switch (node)
        {
            case YamlScalarNode scalar:
                sb.Append(pad).AppendLine(Scalar(scalar));
                break;

            case YamlSequenceNode sequence:
                if (sequence.Children.Count == 0)
                    sb.Append(pad).AppendLine("[]");
                foreach (var item in sequence.Children)
                {
                    sb.Append(pad).AppendLine("-");
                    Write(item, sb, indent + 2);
                }
                break;

            case YamlMappingNode mapping:
                if (mapping.Children.Count == 0)
                    sb.Append(pad).AppendLine("{}");
                foreach (var (key, value) in mapping.Children.OrderBy(kv => Scalar((YamlScalarNode)kv.Key), StringComparer.Ordinal))
                {
                    sb.Append(pad).Append(Scalar((YamlScalarNode)key)).AppendLine(":");
                    Write(value, sb, indent + 2);
                }
                break;
        }
    }

    private static string Scalar(YamlScalarNode scalar) =>
        scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted
            ? $"'{scalar.Value}'"
            : scalar.Value ?? "~";
}
