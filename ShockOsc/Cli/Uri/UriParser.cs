namespace OpenShock.ShockOsc.Cli.Uri;

public static class UriParser
{
    public static UriParameter Parse(string uri)
    {
        ReadOnlySpan<char> uriSpan = uri;
        var dePrefixed = uriSpan[9..];
        
        var getEnd = dePrefixed.IndexOf('/');
        if(getEnd == -1) getEnd = dePrefixed.Length;
        
        var type = dePrefixed[..getEnd];

        var hasArgumentLength = dePrefixed.Length > type.Length + 1;
        
        return new UriParameter
        {
            Type = Enum.Parse<UriParameterType>(type, true),
            Arguments = hasArgumentLength ? dePrefixed[(type.Length + 1)..].ToString().Split('/') : []
        };
    }
}