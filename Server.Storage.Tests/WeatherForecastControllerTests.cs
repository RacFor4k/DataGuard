using Server.Storage.Controllers;

namespace Server.Storage.Tests;

public class WeatherForecastControllerTests
{
    [Fact]
    public void Get_ReturnsFiveForecastsWithinExpectedTemperatureRange()
    {
        var controller = new WeatherForecastController();

        var forecasts = controller.Get().ToArray();

        Assert.Equal(5, forecasts.Length);
        Assert.All(forecasts, forecast => Assert.InRange(forecast.TemperatureC, -20, 54));
        Assert.All(forecasts, forecast => Assert.False(string.IsNullOrWhiteSpace(forecast.Summary)));
    }
}