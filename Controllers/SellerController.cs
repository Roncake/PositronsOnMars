﻿using System.Security.Cryptography;
using DatabaseConnector;
using Microsoft.AspNetCore.Mvc;
using PositronsOnMars.Models;

namespace PositronsOnMars.Controllers;

[ApiController]
[Route("api/Sellers")]
public class SellerController : ControllerBase
{
    /// <summary>
    /// Step 1: Validate all fields |
    /// Step 2: Authenticate user |
    /// Step 3: Generate product ID |
    /// Step 4: Store to database |
    /// Step 5: Return item ID to user.
    /// </summary>
    /// <param name="item">Contains information about the item being sold.</param>
    /// <returns>Returns an HTTP status code and an Int64 (ID).</returns>
    [HttpPost]
    [Route("ListNewItem")]
    public async Task<ActionResult> ListNewItem([FromBody] SellRequest item)
    {
        // Step 1
        if (item.Token is null or "") return Unauthorized();
        // ID is autogenerated
        if (item.Type is 0) return BadRequest();
        if (item.Name is null or "") return BadRequest();
        // Seller is determined using token
        // Image is optional
        if (item.Condition is 0) return BadRequest();
        if (item.Price < 0) return BadRequest();

        // Step 2
        IConnector connector = new Connector();
        var query = "SELECT * FROM tokens WHERE Token = @Token LIMIT 1";
        List<DbObjectToken> results = await connector.QueryAsync<DbObjectToken, dynamic>(query, new
        {
            Token = item.Token
        });
        if (results.Count is 0) return Unauthorized();
        if (DateTime.UtcNow >= results[0].Expiry) return Unauthorized();

        // Step 3
        // Generate a 64-bit integer using RandomNumberGenerator.
        // The chance that the same 64-bit ID is generated twice is very low.
        // Checking the database to see if the ID exists will bring a performance
        // penalty. https://en.wikipedia.org/wiki/Ostrich_algorithm
        var @long = new byte[64 / 8];
        var rng = RandomNumberGenerator.Create();
        rng.GetBytes(@long);
        long id = Math.Abs(BitConverter.ToInt64(@long));

        // Step 4
        query = "INSERT INTO items (Id, Type, Name, Seller, Image, Condition, Price) VALUES (@Id, @Type, @Name, @Seller, @Image, @Condition, @Price)";
        await connector.ExecuteAsync(query, new
        {
            Id = id,
            Type = item.Type,
            Name = item.Name,
            Seller = results[0].Username,
            Image = item.Image is null or "" ? "[ NONE ]" : item.Image,
            Condition = item.Condition,
            Price = Math.Round(item.Price, 2, MidpointRounding.AwayFromZero)
        });

        // Step 5
        return Ok(id);
    }

    /// <summary>
    /// Step 1: Search database for ID |
    /// Step 2: Return the matching item if exists.
    /// </summary>
    /// <param name="id">Stores the ID of the user-requested item.</param>
    /// <returns>Returns an HTTP status code and a 'DbObjectItem' (user-requested item).</returns>
    [HttpGet]
    [Route("GetById/{id:long}")]
    public async Task<ActionResult> GetById([FromRoute] long id)
    {
        // Step 1
        IConnector connector = new Connector();
        const string query = "SELECT * FROM items WHERE Id = @Id LIMIT 1";
        List<DbObjectItem> results = await connector.QueryAsync<DbObjectItem, dynamic>(query, new
        {
            Id = id
        });

        // Step 2
        return results.Count is 0 ? NotFound() : Ok(results[0]);
    }

    /// <summary>
    /// Step 1: Search database for item(s) containing 'search' in 'Name' |
    /// Step 2: Return matching item(s) if exists.
    /// </summary>
    /// <param name="search">Stores the user-provided search term.</param>
    /// <returns>Returns an HTTP status code and a 'List' of type 'DbObjectItem' (list of items matching search).</returns>
    [HttpPut]
    [Route("GetBySearch")]
    public async Task<ActionResult> GetBySearch([FromBody] string search)
    {
        // Step 1
        IConnector connector = new Connector();
        const string query = "SELECT * FROM items WHERE Name LIKE %@Name%";
        List<DbObjectItem> results = await connector.QueryAsync<DbObjectItem, dynamic>(query, new
        {
            Name = search
        });

        // Step 2
        return results.Count is 0 ? NotFound() : Ok(results);
    }

    /// <summary>
    /// Step 1: Validate user input |
    /// Step 2: Select all items in category |
    /// Step 3: Return to user.
    /// </summary>
    /// <param name="type">Stores the user-requested category.</param>
    /// <returns>Returns an HTTP status code and a 'List' of type 'DbObjectItem' (list of items in category).</returns>
    [HttpGet]
    [Route("GetByCategory/{type}")]
    public async Task<ActionResult> GetByCategory([FromRoute] sbyte type)
    {
        // Step 1
        if (type is < -2 or > 6) return BadRequest();

        // Step 2
        IConnector connector = new Connector();
        const string query = "SELECT * FROM items WHERE Type = @Type";
        List<DbObjectItem> results = await connector.QueryAsync<DbObjectItem, dynamic>(query, new
        {
            Type = type
        });

        // Step 3
        return results.Count is 0 ? NotFound() : Ok(results);
    }
}