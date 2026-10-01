using DriverTime.Application.CardReader;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/card-reader")]
public class CardReaderController : ControllerBase
{
    private readonly ICardReadSessionService _cardReadSessionService;
    private readonly ICurrentUserService _currentUser;

    public CardReaderController(
        ICardReadSessionService cardReadSessionService,
        ICurrentUserService currentUser)
    {
        _cardReadSessionService = cardReadSessionService;
        _currentUser = currentUser;
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<CardReadSessionDto>>> GetSessions(
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var sessions = await _cardReadSessionService.GetRecentAsync(
            _currentUser.CompanyId,
            cancellationToken);

        return Ok(sessions);
    }

    [HttpPost("sessions/start")]
    public async Task<ActionResult<CardReadSessionDto>> StartSession(
        StartCardReadSessionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var session = await _cardReadSessionService.StartAsync(
            _currentUser.CompanyId,
            _currentUser.UserId,
            request ?? new StartCardReadSessionRequest(),
            cancellationToken);

        return Ok(session);
    }

    [HttpPost("sessions/{id:guid}/complete")]
    public async Task<ActionResult<CardReadSessionDto>> CompleteSession(
        Guid id,
        CompleteCardReadSessionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var session = await _cardReadSessionService.CompleteAsync(
            _currentUser.CompanyId,
            id,
            request ?? new CompleteCardReadSessionRequest(),
            cancellationToken);

        return session is null ? NotFound() : Ok(session);
    }

    [HttpPost("sessions/{id:guid}/fail")]
    public async Task<ActionResult<CardReadSessionDto>> FailSession(
        Guid id,
        FailCardReadSessionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var session = await _cardReadSessionService.FailAsync(
            _currentUser.CompanyId,
            id,
            request ?? new FailCardReadSessionRequest(),
            cancellationToken);

        return session is null ? NotFound() : Ok(session);
    }

    private bool HasCompanyContext()
    {
        return _currentUser.IsAuthenticated && _currentUser.CompanyId != Guid.Empty;
    }
}
