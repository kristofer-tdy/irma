using System.Diagnostics;
using Flir.Irma.WebApi.Data;
using Flir.Irma.WebApi.Infrastructure;
using Flir.Irma.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flir.Irma.WebApi.Controllers;

[ApiController]
[Route("v1/irma")]
public class SystemController(
    ApplicationMetadata metadata,
    IrmaDbContext dbContext,
    IWebHostEnvironment environment,
    ILogger<SystemController> logger)
    : ControllerBase
{
    [HttpGet("healthz")]
    [ProducesResponseType(typeof(HealthResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var dependencies = new List<HealthDependencyDto>();
        var dbStatus = "Healthy";
        string? dbMessage = null;

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
            dbStatus = "Unhealthy";
            dbMessage = ex.Message;
        }

        dependencies.Add(new HealthDependencyDto
        {
            Name = "SqlDatabase",
            Status = dbStatus,
            Message = dbMessage
        });

        // Placeholder for future Azure AI dependency checks.
        dependencies.Add(new HealthDependencyDto
        {
            Name = "AzureAIFoundry",
            Status = "Unknown",
            Message = "TODO: Implement connectivity checks once Azure AI Foundry integration exists."
        });

        var unhealthyDependency = dependencies.Any(d => string.Equals(d.Status, "Unhealthy", StringComparison.OrdinalIgnoreCase));
        var degradedDependency = dependencies.Any(d => string.Equals(d.Status, "Degraded", StringComparison.OrdinalIgnoreCase));

        var status = unhealthyDependency
            ? "Unhealthy"
            : degradedDependency
                ? "Degraded"
                : "Healthy";

        var uptimeSeconds = (long)DateTimeOffset.UtcNow.Subtract(metadata.StartedAt).TotalSeconds;

        var response = new HealthResponseDto
        {
            Status = status,
            UptimeSeconds = uptimeSeconds,
            Dependencies = dependencies,
            TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        };

        var httpStatus = unhealthyDependency ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK;

        return StatusCode(httpStatus, response);
    }

    [HttpGet("version")]
    [ProducesResponseType(typeof(VersionResponseDto), StatusCodes.Status200OK)]
    public IActionResult GetVersion()
    {
        var response = new VersionResponseDto
        {
            Version = metadata.Version,
            Commit = metadata.Commit,
            BuildDate = metadata.BuildDate,
            Runtime = metadata.Runtime,
            Environment = environment.EnvironmentName
        };

        return Ok(response);
    }
}
