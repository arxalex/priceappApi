﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using priceapp.Services.Interfaces;

namespace priceapp.API.Controllers;

[ApiController]
[Authorize]
[Route("[controller]")]
public class ShopsController : ControllerBase
{
    private readonly IShopsService _shopsService;

    public ShopsController(IShopsService shopsService)
    {
        _shopsService = shopsService;
    }

    [HttpGet("")]
    [Authorize(Roles = "0, 1, 2, 3, 4, 5, 6, 7, 8, 9")]
    public async Task<IActionResult> GetShopsAsync()
    {
        return Ok(await _shopsService.GetShopsAsync());
    }
}