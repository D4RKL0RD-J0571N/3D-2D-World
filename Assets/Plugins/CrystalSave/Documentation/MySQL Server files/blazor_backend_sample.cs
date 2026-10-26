#if !UNITY_5_3_OR_NEWER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using MySql.Data.MySqlClient;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Data.Common;
using Arawn.CrystalSave.Samples;

// Minimal Crystal Save web API for ASP.NET Core/Blazor Server.
// Routes: /save, /load, /delete, /metadata, /list
// Shared-key example for trusted clients. A public service must also authorize
// the authenticated user's uid on every endpoint.

var builder = WebApplication.CreateBuilder(args);

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin() // For development; restrict in production
            .AllowAnyMethod()
            .WithHeaders("Content-Type", "X-API-KEY"));
});

var app = builder.Build();

app.UseCors();

string connString = builder.Configuration.GetConnectionString("MySql")
    ?? "Server=127.0.0.1;Database=game;Uid=user;Pwd=secret;";
string apiKey = builder.Configuration["ApiKey"] ?? "SUPER_SECRET_TOKEN";
string imgRoot = Path.GetFullPath(builder.Configuration["ImageRoot"] ?? Path.Combine(AppContext.BaseDirectory, "img"));
Directory.CreateDirectory(imgRoot);

// simple gate
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Method == HttpMethods.Options)
    {
        await next.Invoke();
        return;
    }

    if (apiKey != string.Empty &&
        ctx.Request.Headers["X-API-KEY"] != apiKey)
    {   ctx.Response.StatusCode = 401; return; }

    await next.Invoke();
});

app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(imgRoot), RequestPath = "/img" });

app.MapPost("/save", async (SavePayload payload) =>
{
    if (!Contract.ValidKey(payload.uid, payload.slot)) return Results.BadRequest();
    byte[] data;
    try { data = Convert.FromBase64String(payload.data); }
    catch (Exception error) when (error is FormatException or ArgumentNullException) { return Results.BadRequest(); }
    if (data.Length == 0) return Results.BadRequest();
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand("INSERT INTO CrystalSaveData (UserID,Slot,Data) VALUES (@uid,@slot,@data) ON DUPLICATE KEY UPDATE Data=@data", conn);
    cmd.Parameters.AddWithValue("@uid", payload.uid);
    cmd.Parameters.AddWithValue("@slot", payload.slot);
    cmd.Parameters.AddWithValue("@data", data);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok();
});

app.MapPost("/metadata", async (MetadataPayload payload) =>
{
    if (!Contract.ValidKey(payload.uid, payload.slot) || payload.ticks < 0 || payload.ticks > DateTime.MaxValue.Ticks)
        return Results.BadRequest();
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand("INSERT INTO CrystalSaveData (UserID,Slot,Data,SlotName,LastSavedTicks,LastActiveScene,ScreenshotFileName,CustomMetadata) " +
        "VALUES (@uid,@slot,'',@name,@ticks,@scene,@shot,@meta) ON DUPLICATE KEY UPDATE SlotName=@name,LastSavedTicks=@ticks," +
        "LastActiveScene=@scene,ScreenshotFileName=@shot,CustomMetadata=@meta", conn);
    cmd.Parameters.AddWithValue("@name", payload.name ?? $"Slot {payload.slot}");
    cmd.Parameters.AddWithValue("@ticks", payload.ticks);
    cmd.Parameters.AddWithValue("@scene", payload.scene ?? "");
    cmd.Parameters.AddWithValue("@shot", payload.shot ?? "");
    cmd.Parameters.AddWithValue("@meta", JsonSerializer.Serialize(payload.meta ?? new Dictionary<string,string?>()));
    cmd.Parameters.AddWithValue("@uid", payload.uid);
    cmd.Parameters.AddWithValue("@slot", payload.slot);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok();
});

app.MapGet("/metadata", async (string uid, int slot) =>
{
    if (!Contract.ValidKey(uid, slot)) return Results.BadRequest();
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    using var cmd = new MySqlCommand(Contract.MetadataSelect + " WHERE UserID=@uid AND Slot=@slot", conn);
    cmd.Parameters.AddWithValue("@uid", uid);
    cmd.Parameters.AddWithValue("@slot", slot);
    using var reader = await cmd.ExecuteReaderAsync();
    return await reader.ReadAsync() ? Results.Json(Contract.ReadMetadata(reader)) : Results.NotFound();
});

app.MapGet("/load", async (string uid, int slot) =>
{
    if (!Contract.ValidKey(uid, slot)) return Results.BadRequest();
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand("SELECT Data FROM CrystalSaveData WHERE UserID=@uid AND Slot=@slot", conn);
    cmd.Parameters.AddWithValue("@uid", uid);
    cmd.Parameters.AddWithValue("@slot", slot);
    var result = await cmd.ExecuteScalarAsync();
    return result != null ? Results.Text(Convert.ToBase64String((byte[])result)) : Results.NotFound();
});

app.MapPost("/delete", async (SaveKey key) =>
{
    if (!Contract.ValidKey(key.uid, key.slot)) return Results.BadRequest();
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    using var image = new MySqlCommand("SELECT ScreenshotFileName FROM CrystalSaveData WHERE UserID=@uid AND Slot=@slot", conn);
    image.Parameters.AddWithValue("@uid", key.uid);
    image.Parameters.AddWithValue("@slot", key.slot);
    string? path = Contract.ImagePath(imgRoot, key.uid, (await image.ExecuteScalarAsync()) as string);
    if (path != null && File.Exists(path)) File.Delete(path);
    using var cmd = new MySqlCommand("DELETE FROM CrystalSaveData WHERE UserID=@uid AND Slot=@slot", conn);
    cmd.Parameters.AddWithValue("@uid", key.uid);
    cmd.Parameters.AddWithValue("@slot", key.slot);
    await cmd.ExecuteNonQueryAsync();
    return Results.Ok();
});

app.MapGet("/list", async (string uid) =>
{
    if (string.IsNullOrEmpty(uid)) return Results.BadRequest();
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    using var cmd = new MySqlCommand(Contract.MetadataSelect + " WHERE UserID=@uid", conn);
    cmd.Parameters.AddWithValue("@uid", uid);
    using var reader = await cmd.ExecuteReaderAsync();
    var rows = new List<SlotMeta>();
    while (await reader.ReadAsync())
    {
        rows.Add(Contract.ReadMetadata(reader));
    }
    return Results.Json(rows);
});

app.MapPost("/uploadImage", async (HttpRequest req) =>
{
    if (!req.HasFormContentType) return Results.BadRequest();

    var form = await req.ReadFormAsync();
    IFormFile? file = form.Files["shot"];
    string uid   = form["uid"].ToString();
    string slot  = form["slot"].ToString();
    string nameF = form["name"].ToString();

    if (file is null || !int.TryParse(slot, out int number) || !Contract.ValidKey(uid, number))
        return Results.BadRequest();

    string ext      = Path.GetExtension(file.FileName).ToLowerInvariant();
    string name     = !string.IsNullOrEmpty(nameF) ? Path.GetFileName(nameF) : $"{slot}{ext}";
    string? path    = Contract.ImagePath(imgRoot, uid, name);
    if (path == null) return Results.BadRequest();
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);

    await using (var stream = File.Create(path)) await file.CopyToAsync(stream);

    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    var cmd = new MySqlCommand(
        "UPDATE CrystalSaveData " +
        "SET ScreenshotFileName=@n " +
        "WHERE UserID=@u AND Slot=@s", conn);
    cmd.Parameters.AddWithValue("@n", name);
    cmd.Parameters.AddWithValue("@u", uid);
    cmd.Parameters.AddWithValue("@s", int.Parse(slot));
    await cmd.ExecuteNonQueryAsync();

    return Results.Ok();
});

app.Run();

namespace Arawn.CrystalSave.Samples
{
    public partial record SavePayload(string uid, int slot, string data);
    public partial record MetadataPayload(string uid, int slot, string? name, long ticks, string? scene, string? shot, Dictionary<string,string?>? meta);
    public partial record SaveKey(string uid, int slot);
    public partial record SlotMeta(int slot, string name, long ticks, string scene, string shot, MetaPair[] meta);
    public record MetaPair(string key, string? value);

    static class Contract
    {
        internal const string MetadataSelect = "SELECT Slot AS slot,SlotName AS name,LastSavedTicks AS ticks," +
            "LastActiveScene AS scene,ScreenshotFileName AS shot,CustomMetadata AS meta FROM CrystalSaveData";
        internal static bool ValidKey(string uid, int slot) => !string.IsNullOrEmpty(uid) && slot > 0;
        internal static SlotMeta ReadMetadata(DbDataReader reader)
        {
            string Text(string name, string fallback = "") => reader.IsDBNull(reader.GetOrdinal(name)) ? fallback : reader.GetString(reader.GetOrdinal(name));
            int slot = reader.GetInt32(reader.GetOrdinal("slot"));
            var metadata = JsonSerializer.Deserialize<Dictionary<string,string?>>(Text("meta", "{}")) ?? new();
            return new SlotMeta(slot, Text("name", $"Slot {slot}"),
                reader.IsDBNull(reader.GetOrdinal("ticks")) ? 0 : reader.GetInt64(reader.GetOrdinal("ticks")),
                Text("scene"), Text("shot"), metadata.Select(pair => new MetaPair(pair.Key, pair.Value)).ToArray());
        }
        internal static string? ImagePath(string root, string uid, string? name)
        {
            if (string.IsNullOrEmpty(name) || uid.Split('/', '\\').Any(part => part is "" or "." or "..") || uid.Contains('\0')) return null;
            return Path.Combine(root, uid.Replace('\\', '/'), Path.GetFileName(name.Replace('\\', '/')));
        }
    }
}
#endif
