namespace deepdeepbimapi.Models;

public record CreateUserRequest
(
    string FirstName,
    string LastName,
    string Password,
    string Email
);