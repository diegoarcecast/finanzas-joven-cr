﻿namespace auth.api.Contracts;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string? FirstName,
    string? LastName
);
