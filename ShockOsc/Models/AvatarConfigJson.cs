namespace ShockLink.ShockOsc.Models;

public class AvatarConfigJson
{
    public string id { get; set; }
    public string name { get; set; }
    public List<Parameter> parameters { get; set; }
}

public class Parameter
{
    public string name { get; set; }
    public InputOutput input { get; set; }
    public InputOutput output { get; set; }
}

public class InputOutput
{
    public string address { get; set; }
    public string type { get; set; }
}