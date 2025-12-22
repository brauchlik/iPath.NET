using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace iPath.Ollama.UI.Plugins;





public class HomePlugin(List<LightModel> lights)
{

    [KernelFunction("get_lights")]
    [Description("Gets a list of lights and their current state")]
    [return: Description("An array of lights")]
    public Task<List<LightModel>> GetLightsAsync()
    {
        return Task.FromResult(lights);
    }

    [KernelFunction("change_state")]
    [Description("Changes the state of the light.If  Turn on or turn off lights state.This function changes the state when it toggles a specific light type on and off. ")]
    [return: Description("The update state of the light: will return if the light does not exist")]
    public Task<LightModel?> ChangeStateAsync(
        [Description("This is the ID of the light")]
        int id,
        [Description("True  if the light is on ; false if the light is off")]
        bool isOn,
        [Description("The Color of the light as html color name")]
        string color)
    {
        var light = lights.FirstOrDefault(light => light.Id == id);

        if (light == null)
        {
            return null;
        }

        // Update the light with the new state
        light.IsOn = isOn;
        light.Color = color;

        return Task.FromResult(light);
    }
}

public class LightModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("is_on")]
    public bool? IsOn { get; set; }

    public string Color { get; set; } = "yellow";

}