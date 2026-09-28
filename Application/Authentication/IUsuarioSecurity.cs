using Application.DTO.Response;
using Domain.Entities;
namespace Application.Authentication;
public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string? hash);
}
public interface ITokenService
{
    LoginResponse Create(Usuario usuario);
}
