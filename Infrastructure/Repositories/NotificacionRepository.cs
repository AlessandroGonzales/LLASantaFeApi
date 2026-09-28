using Domain.Models;
using Domain.Repositories;
using Npgsql;
using NpgsqlTypes;
namespace Infrastructure.Repositories;
public sealed class NotificacionRepository(NpgsqlDataSource source) : INotificacionRepository
{
    public async Task<NotificacionEstado?> EncolarAsync(string tipo,Guid referencia,CancellationToken ct)
    {
        await using var connection=await source.OpenConnectionAsync(ct);
        await using var enqueue=new NpgsqlCommand("SELECT public.encolar_correo($1,$2)",connection);
        enqueue.Parameters.AddWithValue(tipo);enqueue.Parameters.AddWithValue(referencia);
        var value=await enqueue.ExecuteScalarAsync(ct);
        if (value is not Guid id) return null;
        await using var state=new NpgsqlCommand("SELECT estado FROM public.correos_transaccionales WHERE id=$1",connection);
        state.Parameters.AddWithValue(id);
        return new(id,(string)(await state.ExecuteScalarAsync(ct))!);
    }
    public async Task<CorreoPendiente?> ReservarAsync(int limiteDiario,CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("SELECT id,tipo,email,intentos FROM public.reservar_correo($1)");
        cmd.Parameters.AddWithValue(limiteDiario);
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetInt32(3)) : null;
    }
    public async Task FinalizarAsync(Guid id,string estado,string? proveedorId,string? error,int esperaSegundos,CancellationToken ct)
    {
        await using var cmd=source.CreateCommand("""
            UPDATE public.correos_transaccionales SET estado=$2,proveedor_id=$3,error_codigo=$4,
             enviado_at=CASE WHEN $2='enviado' THEN now() ELSE NULL END,
             disponible_at=now()+make_interval(secs=>$5)
            WHERE id=$1 AND estado='procesando'
            """);
        cmd.Parameters.AddWithValue(id);cmd.Parameters.AddWithValue(estado);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Text,(object?)proveedorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Text,(object?)error ?? DBNull.Value);
        cmd.Parameters.AddWithValue(esperaSegundos);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
