using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode.ReverseEngineering;
using YoutubeExplode.ReverseEngineering.Responses;

namespace YoutubeExplode.Videos.ClosedCaptions
{
    /// <summary>
    /// Queries related to closed captions of YouTube videos.
    /// </summary>
    public class ClosedCaptionClient
    {
        private readonly YoutubeHttpClient _httpClient;

        /// <summary>
        /// Initializes an instance of <see cref="ClosedCaptionClient"/>.
        /// </summary>
        internal ClosedCaptionClient(YoutubeHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Gets the manifest that contains information about available closed caption tracks in the specified video.
        /// </summary>
        public async Task<ClosedCaptionManifest> GetManifestAsync(VideoId videoId)
        {
            var videoInfoResponse = await VideoInfoResponse.GetAsync(_httpClient, videoId);
            var playerResponse = videoInfoResponse.GetPlayerResponse();

            var tracks = playerResponse
                .GetClosedCaptionTracks()
                .Select(track => new ClosedCaptionTrackInfo(
                    track.GetUrl(),
                    new Language(
                        track.GetLanguageCode(),
                        track.GetLanguageName()
                    ),
                    track.IsAutoGenerated()
                )).ToArray();

            return new ClosedCaptionManifest(tracks);
        }

        /// <summary>
        /// Gets the actual closed caption track which is identified by the specified metadata.
        /// </summary>
        public async Task<ClosedCaptionTrack> GetAsync(ClosedCaptionTrackInfo trackInfo)
        {
            var response = await ClosedCaptionTrackResponse.GetAsync(_httpClient, trackInfo.Url);

            var captions = response.GetClosedCaptions()
                .Where(t => !string.IsNullOrWhiteSpace(t.GetText()))
                .Select(t => new ClosedCaption(
                    t.GetText(),
                    t.GetOffset(),
                    t.GetDuration()
                )).ToArray();

            return new ClosedCaptionTrack(captions);
        }

        /// <summary>
        /// Writes the actual closed caption track which is identified by the specified metadata to the specified writer.
        /// Closed captions are written in the SRT file format.
        /// </summary>
        public async Task WriteToAsync(ClosedCaptionTrackInfo trackInfo, TextWriter writer,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var track = await GetAsync(trackInfo);

            var buffer = new StringBuilder();
            for (var i = 0; i < track.Captions.Count; i++)
            {
                var caption = track.Captions[i];
                buffer.Clear();

                cancellationToken.ThrowIfCancellationRequested();

                // Line number
                buffer.AppendLine((i + 1).ToString());

                // Time start --> time end
                buffer.Append(caption.Offset.ToString(@"hh\:mm\:ss\,fff"));
                buffer.Append(" --> ");
                buffer.Append((caption.Offset + caption.Duration).ToString(@"hh\:mm\:ss\,fff"));
                buffer.AppendLine();

                // Actual text
                buffer.AppendLine(caption.Text);

                await writer.WriteLineAsync(buffer.ToString());
                progress?.Report((i + 1.0) / track.Captions.Count);
            }
        }

        /// <summary>
        /// Downloads the actual closed caption track which is identified by the specified metadata to the specified file.
        /// Closed captions are written in the SRT file format.
        /// </summary>
        public async Task DownloadAsync(ClosedCaptionTrackInfo trackInfo, string filePath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            using var writer = File.CreateText(filePath);
            await WriteToAsync(trackInfo, writer, progress, cancellationToken);
        }
    }
}