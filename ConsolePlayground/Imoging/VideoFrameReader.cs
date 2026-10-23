using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using SkiaSharp;

namespace ConsolePlayground.Cmd.Imoging;

public sealed unsafe class VideoFrameReader : IDisposable {
    private AVFormatContext* _fmt;
    private AVCodecContext* _codec;
    private AVFrame* _frame;
    private AVPacket* _packet;
    private SwsContext* _sws;
    private readonly int _streamIndex;
    private readonly double _timeBase;
    private bool _flushed;

    public int Width { get; }
    public int Height { get; }

    public double FrameRate { get; }

    public VideoFrameReader(string path) {
        var fmt = (AVFormatContext*)null;
        Check(ffmpeg.avformat_open_input(&fmt, path, null, null));
        _fmt = fmt;
        try {
            Check(ffmpeg.avformat_find_stream_info(_fmt, null));

            // 映像ストリームを探す
            AVCodec* decoder = null;
            _streamIndex = Check(ffmpeg.av_find_best_stream(
                _fmt, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0));
            var stream = _fmt->streams[_streamIndex];
            _timeBase = ffmpeg.av_q2d(stream->time_base);

            // フレームレート取得
            var fr = ffmpeg.av_guess_frame_rate(_fmt, stream, null);
            if (fr.num == 0 || fr.den == 0) {
                fr = stream->avg_frame_rate;
            }
            FrameRate = (fr.num != 0 && fr.den != 0) ? ffmpeg.av_q2d(fr) : 0;

            // デコーダ初期化
            _codec = ffmpeg.avcodec_alloc_context3(decoder);
            if (_codec == null) {
                throw new InvalidOperationException("avcodec_alloc_context3 failed");
            }
            Check(ffmpeg.avcodec_parameters_to_context(_codec, stream->codecpar));
            _codec->thread_count = 0;
            _codec->thread_type = ffmpeg.FF_THREAD_FRAME | ffmpeg.FF_THREAD_SLICE;
            Check(ffmpeg.avcodec_open2(_codec, decoder, null));

            Width = _codec->width;
            Height = _codec->height;

            _frame = ffmpeg.av_frame_alloc();
            _packet = ffmpeg.av_packet_alloc();
            if (_frame == null || _packet == null) {
                throw new InvalidOperationException("av_frame_alloc/av_packet_alloc failed");
            }
        }
        catch {
            Dispose();
            throw;
        }
    }


    public double DurationSeconds =>
        _fmt->duration == ffmpeg.AV_NOPTS_VALUE ? 0 : _fmt->duration / (double)ffmpeg.AV_TIME_BASE;


    /// <summary>
    /// 次のフレームをSKBitmapとして取得
    /// </summary>
    /// <param name="bitmap"></param>
    /// <param name="timestamp"></param>
    /// <returns>終端ならfalse</returns>
    public bool TryReadFrame(out SKBitmap? bitmap, out TimeSpan timestamp) {
        bitmap = null;
        if (!DecodeNextFrame(out timestamp)) return false;
        bitmap = ConvertToBitmap();
        return true;
    }


    /// <summary>
    /// 指定時刻へシーク
    /// </summary>
    /// <param name="target"></param>
    /// <param name="bitmap"></param>
    /// <param name="timestamp"></param>
    /// <param name="accurate">falseなら直前のキーフレームをそのまま返す(高速)</param>
    /// <returns></returns>
    public bool TrySeek(TimeSpan target, out SKBitmap? bitmap, out TimeSpan timestamp, bool accurate = true) {
        bitmap = null;
        timestamp = default;

        if (target < TimeSpan.Zero) {
            target = TimeSpan.Zero;
        }

        // ストリームのtime_base単位へ変換
        long targetPts = (long)(target.TotalSeconds / _timeBase);
        Check(ffmpeg.av_seek_frame(_fmt, _streamIndex, targetPts, ffmpeg.AVSEEK_FLAG_BACKWARD));
        ffmpeg.avcodec_flush_buffers(_codec);
        _flushed = false;

        while (DecodeNextFrame(out timestamp)) {
            // 正確モードは目的時刻に届くまで変換せず読み捨てる
            if (!accurate || _frame->best_effort_timestamp >= targetPts) {
                bitmap = ConvertToBitmap();
                return true;
            }
        }
        return false; // 目的時刻が動画の終端を超えてる
    }

    private bool DecodeNextFrame(out TimeSpan timestamp) {
        timestamp = default;

        while (true) {
            int ret = ffmpeg.avcodec_receive_frame(_codec, _frame);
            if (ret == 0) {
                long pts = _frame->best_effort_timestamp;
                timestamp = pts == ffmpeg.AV_NOPTS_VALUE
                    ? TimeSpan.Zero
                    : TimeSpan.FromSeconds(pts * _timeBase);
                return true;
            }
            if (ret == ffmpeg.AVERROR_EOF) return false;
            if (ret != ffmpeg.AVERROR(ffmpeg.EAGAIN)) Check(ret);

            ret = ffmpeg.av_read_frame(_fmt, _packet);
            if (ret < 0) {
                if (_flushed) return false;
                _flushed = true;
                Check(ffmpeg.avcodec_send_packet(_codec, null));
                continue;
            }

            try {
                if (_packet->stream_index == _streamIndex) {
                    ret = ffmpeg.avcodec_send_packet(_codec, _packet);
                    // 壊れたパケット読み飛ばし
                    if (ret != ffmpeg.AVERROR_INVALIDDATA) {
                        Check(ret);
                    }
                }
            }
            finally {
                ffmpeg.av_packet_unref(_packet);
            }
        }
    }

    private SKBitmap ConvertToBitmap() {
        int width = _frame->width;
        int height = _frame->height;
        var format = (AVPixelFormat)_frame->format;
        if (width <= 0 || height <= 0 || format == AVPixelFormat.AV_PIX_FMT_NONE) {
            throw new InvalidOperationException("フレームの寸法または画素形式が不正");
        }
        _sws = ffmpeg.sws_getCachedContext(_sws,
            width, height, format,
            width, height, AVPixelFormat.AV_PIX_FMT_BGRA,
            (int)SwsFlags.SWS_BILINEAR, null, null, null);
        if (_sws == null) {
            throw new InvalidOperationException("sws_getCachedContextが失敗");
        }

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        var bmp = new SKBitmap(info);

        var dstData = new byte_ptrArray4();
        var dstLinesize = new int_array4();
        dstData[0] = (byte*)bmp.GetPixels();
        dstLinesize[0] = bmp.RowBytes;

        ffmpeg.sws_scale(_sws, _frame->data, _frame->linesize, 0, height, dstData, dstLinesize);
        return bmp;
    }

    private static int Check(int ret) {
        if (ret >= 0) return ret;
        var buf = stackalloc byte[1024];
        ffmpeg.av_strerror(ret, buf, 1024);
        throw new InvalidOperationException(Marshal.PtrToStringAnsi((IntPtr)buf));
    }


    public void Dispose() {
        if (_sws != null) { ffmpeg.sws_freeContext(_sws); _sws = null; }
        var frame = _frame; if (frame != null) { ffmpeg.av_frame_free(&frame); _frame = null; }
        var packet = _packet; if (packet != null) { ffmpeg.av_packet_free(&packet); _packet = null; }
        var codec = _codec; if (codec != null) { ffmpeg.avcodec_free_context(&codec); _codec = null; }
        var fmt = _fmt; if (fmt != null) { ffmpeg.avformat_close_input(&fmt); _fmt = null; }
    }
}
