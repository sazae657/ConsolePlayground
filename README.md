コンソール遊び

```
ConsolePlayground [options]

オプション:
  -m, --movie <movie>                  動画またはウインドウまたはモニター
  --renderer <renderer>                描画方式: ascii か sixel [default: ascii]
  -c, --characters <characters>        ASCII表示に使用する文字 [default:  `.:-=;*?+#%@]
  --color                              ASCII文字を着色する
  --fill                               ASCII表示を色付きブロックで埋める
  --full-width                         全角文字でASCII表示する
  --box                                輪郭を罫線素片で描く
  --box-round                          罫線の角を角丸にする(--box用)
  --box-fill                           線の無い所を --characters の文字で明るさに応じて塗る(--box用)
  --box-thin                           太線を使わない(--box用)
  --box-no-diagonal                    斜線 ╱╲ を使わない(--box用)
  --sixel                              sixelで表示(--renderer sixelと等価)
  --infinite                           無限
  -f, --ffmpeg <ffmpeg>                ffmpegのパス
  --sixel-width <sixel-width>          Sixelの最大幅（1～4096ピクセル、省略時は端末に合わせる）
  --sixel-height <sixel-height>        Sixelの最大高さ（1～4096ピクセル、省略時は端末に合わせる）
  --record <record>                    テキストに保存
  --record-format <Base64|BSV|Text>    保存形式 [default: BSV]
  --info                               情報表示
  --color-reduction <color-reduction>  Sixelでまあまあな品質の減色を使う。色数は2～256(省略時256色、死ぬほど重い)
  --listen <listen>                    TCP待ち受け(IP:Port)
  --listen-remote                      ループバック以外での待ち受けを許可する(プレーンテキストでダダ漏れ注意)
  --headless                           レンダリングするだけ(表示しない)
  --headless-width <headless-width>    レンダリングする幅(文字数)
  --headless-height <headless-height>  レンダリングする高さ(文字数)
  --benchmark                          全フレーム全速処理して、終了時に処理時間の内訳を表示
  --list-window                        ウインドウリスト表示
  --list-monitor                       モニターリスト表示
  -?, -h, --help                       Show help and usage information
  --version                            バージョン情報を表示する
  ```