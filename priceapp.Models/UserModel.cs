﻿namespace priceapp.Models;

public class UserModel
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public int Role { get; set; }
    public bool Protected { get; set; }
}