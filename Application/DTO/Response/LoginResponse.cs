namespace Application.DTO.Response;
public sealed record LoginResponse(string Token, DateTime ExpiresAt, string TokenType = "Bearer");
