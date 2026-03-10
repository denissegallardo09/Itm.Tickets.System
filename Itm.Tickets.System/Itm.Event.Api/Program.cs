var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var eventos = new List<Evento>
{
    new Evento(1, "Concierto ITM", 50000, 100)
};

app.MapGet("/api/events/{id}", (int id) => {
    var evento = eventos.FirstOrDefault(e => e.Id == id);
    return evento is not null
        ? Results.Ok(new EventoDto(evento.Id, evento.Nombre, evento.PrecioBase, evento.SillasDisponibles))
        : Results.NotFound();
})
.WithName("GetEventById")
.WithOpenApi();

app.MapPost("/api/events/reserve", (ReservaRequest request) => {
    var evento = eventos.FirstOrDefault(e => e.Id == request.EventId);

    if (evento is null)
        return Results.NotFound();

    if (request.Quantity <= 0 || request.Quantity > evento.SillasDisponibles)
        return Results.BadRequest("No hay suficientes sillas disponibles.");

    var index = eventos.IndexOf(evento);
    eventos[index] = evento with { SillasDisponibles = evento.SillasDisponibles - request.Quantity };

    var updated = eventos[index];
    return Results.Ok(new EventoDto(updated.Id, updated.Nombre, updated.PrecioBase, updated.SillasDisponibles));
})
.WithName("ReserveEvent")
.WithOpenApi();

app.MapPost("/api/events/release", (ReservaRequest request) => {
    var evento = eventos.FirstOrDefault(e => e.Id == request.EventId);

    if (evento is null)
        return Results.NotFound();

    if (request.Quantity <= 0)
        return Results.BadRequest("La cantidad debe ser mayor a cero.");

    var index = eventos.IndexOf(evento);
    eventos[index] = evento with { SillasDisponibles = evento.SillasDisponibles + request.Quantity };

    var updated = eventos[index];
    return Results.Ok(new EventoDto(updated.Id, updated.Nombre, updated.PrecioBase, updated.SillasDisponibles));
})
.WithName("ReleaseEvent")
.WithOpenApi();

app.Run();

