using Microsoft.Extensions.Hosting;
using Snail.MCP.Blender.Application.Sessions;

namespace Snail.MCP.Blender.Application.Skills;

/// <summary>Unloads skills nobody has used for a while, so a long session does not keep every tool in the model's context.</summary>
public sealed class SkillExpiry(ClientSessions clients, SkillExpiryOptions options, TimeProvider timeProvider, ILogger<SkillExpiry> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.IdleTimeout <= TimeSpan.Zero)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.PollInterval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var expired = clients.All.SelectMany(client => client.Skills.ExpireIdle(options.IdleTimeout)).Distinct(StringComparer.Ordinal).ToList();

                if (expired.Count > 0)
                {
                    logger.LogInformation("Unloaded idle skills: {Skills}", string.Join(", ", expired));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}

/// <summary>How long a skill may sit unused and how often that is checked.</summary>
public sealed record SkillExpiryOptions(TimeSpan IdleTimeout, TimeSpan PollInterval)
{
    /// <summary>The expiry a skill-idle setting asks for; zero minutes leaves it switched off.</summary>
    public static SkillExpiryOptions For(int skillIdleMinutes) =>
        new(TimeSpan.FromMinutes(skillIdleMinutes), TimeSpan.FromMinutes(1));
}
