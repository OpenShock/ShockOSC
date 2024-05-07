namespace OpenShock.ShockOsc.Cli.Uri;

public static class UriParser
{
    public static UriParameter Parse(string uri)
    {
        ReadOnlySpan<char> uriSpan = uri;
        var dePrefixed = uriSpan[9..];
        var type = dePrefixed[..dePrefixed.IndexOf('/')];

        return new UriParameter
        {
            Type = Enum.Parse<UriParameterType>(type, true),
            Arguments = dePrefixed[(type.Length + 1)..].ToString().Split('/')
        };
    }
}