namespace deepdeepbimapi.Models;

public record LoginRequest
(
    string Password,
    string Email
);