using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;

namespace SLSKDONET.Services.Tagging
{
    public interface IUniversalCueService
    {
        Task SyncToTagsAsync(string filePath, List<OrbitCue> cues);
        Task ExportToXmlAsync(IEnumerable<PlaylistTrack> tracks);
    }

    public class UniversalCueService : IUniversalCueService
    {
        private readonly ILogger<UniversalCueService> _logger;
        private readonly ISeratoMarkerService _seratoService;

        public UniversalCueService(
            ILogger<UniversalCueService> logger,
            ISeratoMarkerService seratoService)
        {
            _logger = logger;
            _seratoService = seratoService;
        }

        public async Task SyncToTagsAsync(string filePath, List<OrbitCue> cues)
        {
             await _seratoService.WriteMarkersAsync(filePath, cues);
             // await _traktorService.WriteTagsAsync(filePath, cues);
        }

        public Task ExportToXmlAsync(IEnumerable<PlaylistTrack> tracks)
        {
             // Rekordbox XML logic later
             return Task.CompletedTask;
        }
    }
}
