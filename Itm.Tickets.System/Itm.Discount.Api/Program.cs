var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var descuentos = new List<Descuento>
{
    new Descuento("ITM50", 0.5m)
};

app.MapGet("/api/discounts/{code}", (string code) =>
{
    var descuento = descuentos.FirstOrDefault(d => d.Codigo == code);
    return descuento is not null ? Results.Ok(descuento) : Results.NotFound();
})
.WithName("GetDiscountByCode")
.WithOpenApi();

app.Run();

