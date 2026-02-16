namespace Server.Middleware
{
    public class TimeSynchronization
    {
        private readonly RequestDelegate _next;

        public TimeSynchronization(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue("X-Request-Time", out var TimeValues))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid X-Request-Time time");
                return;
            }
            if (!DateTime.TryParse(TimeValues.FirstOrDefault(), out var ClientTime))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid X-Request-Time time format");
                return;
            }
            if (DateTime.UtcNow.Subtract(ClientTime).Hours < 1)
            {
                await _next(context);
                return;
            }
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Client and server times differ by more than 1 hour");
        }
    }   
}
