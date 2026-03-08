var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("EventApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:EventApi"]!);
})
.AddStandardResilienceHandler();

builder.Services.AddHttpClient("DiscountApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:DiscountApi"]!);
})
.AddStandardResilienceHandler();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/bookings", async (BookingRequest request, IHttpClientFactory factory) =>
{
    var eventClient    = factory.CreateClient("EventApi");
    var discountClient = factory.CreateClient("DiscountApi");

    // Paso 1: Validación paralela
    var eventoTask    = eventClient.GetAsync($"/api/events/{request.EventId}");
    var descuentoTask = discountClient.GetAsync($"/api/discounts/{request.DiscountCode}");

    await Task.WhenAll(eventoTask, descuentoTask);

    if (!eventoTask.Result.IsSuccessStatusCode)
        return Results.NotFound($"Evento {request.EventId} no encontrado.");

    if (!descuentoTask.Result.IsSuccessStatusCode)
        return Results.NotFound($"Código de descuento '{request.DiscountCode}' no encontrado.");

    var evento    = await eventoTask.Result.Content.ReadFromJsonAsync<EventoDto>();
    var descuento = await descuentoTask.Result.Content.ReadFromJsonAsync<DescuentoDto>();

    if (request.Tickets <= 0)
        return Results.BadRequest("Cantidad de tickets inválida");
    if (request.Tickets > evento!.SillasDisponibles)
        return Results.BadRequest("Sin disponibilidad.");

    // Paso 2: Matemáticas
    var subtotal = evento.PrecioBase * request.Tickets;
    var total    = subtotal * (1 - descuento!.Porcentaje);

    // Paso 3: Reserva (SAGA Step 1)
    var reservaResponse = await eventClient.PostAsJsonAsync("/api/events/reserve",
        new ReservaDto(request.EventId, request.Tickets));

    if (!reservaResponse.IsSuccessStatusCode)
        return Results.BadRequest("No se pudo reservar las sillas.");

    try
    {
        // Paso 4: Simulación de pago
        var resultado = Random.Shared.Next(1, 11);

        if (resultado <= 5)
            throw new Exception("Pago rechazado por la pasarela.");

        return Results.Ok(new
        {
            EventoNombre   = evento.Nombre,
            Tickets        = request.Tickets,
            Subtotal       = subtotal,
            Descuento      = $"{descuento.Porcentaje * 100}%",
            Total          = total,
            Estado         = "Pago exitoso"
        });
    }
    catch
    {
        // Paso 5: Compensación (SAGA Step 2)
        await eventClient.PostAsJsonAsync("/api/events/release",
            new ReservaDto(request.EventId, request.Tickets));

        return Results.Ok("El pago falló, las sillas fueron liberadas.");
    }
})
.WithName("CreateBooking")
.WithOpenApi();

app.Run();

