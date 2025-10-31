using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Sharp.Shared;

namespace Leader.Precache
{
    internal sealed class ParticlePrecache
    {
        private readonly ILogger<ParticlePrecache> _logger;
        private readonly ISharedSystem _sharedSystem;
        private readonly List<string> _vpcfFiles = new();

        public ParticlePrecache(ILogger<ParticlePrecache> logger, ISharedSystem sharedSystem)
        {
            _logger = logger;
            _sharedSystem = sharedSystem;
        }

        public void Init()
        {
            var gameRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @".."));
            var fullPath = Path.Combine(gameRoot, "custom", "particles");

            _logger.LogInformation("Game root: {Root}", gameRoot);
            _logger.LogInformation("Scanning particle folder: {Path}", fullPath);

            if (Directory.Exists(fullPath))
            {
                _vpcfFiles.AddRange(Directory.GetFiles(fullPath, "*.vpcf", SearchOption.AllDirectories));
                _logger.LogInformation("Found {Count} vpcf files in {Path}", _vpcfFiles.Count, fullPath);
                foreach (var file in _vpcfFiles)
                    _logger.LogInformation(" - {File}", file);
            }
            else
            {
                _logger.LogWarning("Particle folder not found: {Path}", fullPath);
            }
        }

        public void PrecacheAll()
        {
            var modSharp = _sharedSystem.GetModSharp();

            foreach (var file in _vpcfFiles)
            {
                modSharp.PrecacheResource(file);
                _logger.LogInformation("Precaching vpcf: {File}", file);
            }
        }
    }
}