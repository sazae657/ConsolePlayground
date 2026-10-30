コンソール遊び

```
ConsolePlayground [options]

オプション:
  -m, --movie <movie>                            動画またはウインドウまたはモニター
  --renderer <renderer>                          描画方式: ascii か sixel か kitty [default: ascii]
  -c, --characters <characters>                  ASCII表示に使用する文字 [default:  `.:-=;*?+#%@]
  --color                                        ASCII文字を着色する
  --fill                                         ASCII表示を色付きブロックで埋める
  --full-width                                   全角文字でASCII表示する
  --box                                          輪郭を罫線素片で描く
  --box-round                                    罫線の角を角丸にする(--box用)
  --box-fill                                     線の無い所を --characters の文字で明るさに応じて塗る(--box用)
  --box-thin                                     太線を使わない(--box用)
  --box-no-diagonal                              斜線 ╱╲ を使わない(--box用)
  --sixel                                        sixelで表示(--renderer sixelと等価)
  --kitty                                        Kitty Graphics Protocolで表示(--renderer kittyと等価)
  --brightness                                   輝度を使ってグレースケールで表示する
  --brightness-bcgyr                             輝度を使ってBCGYRで表示する
  --infinite                                     無限
  -f, --ffmpeg <ffmpeg>                          ffmpegのパス
  --image-width, --sixel-width <image-width>     画像の最大幅（1～4096ピクセル、省略時は端末に合わせる）
  --image-height, --sixel-height <image-height>  画像の最大高さ（1～4096ピクセル、省略時は端末に合わせる）
  --record <record>                              テキストに保存
  --record-format <Base64|BSV|Text>              保存形式 [default: BSV]
  --info                                         情報表示
  --color-reduction <color-reduction>            まあまあな品質の減色を使う。色数は2～256(省略時256色、死ぬほど重い)
  --listen <listen>                              TCP待ち受け(IP:Port)
  --listen-remote                                ループバック以外での待ち受けを許可する(プレーンテキストでダダ漏れ注意)
  --headless                                     レンダリングするだけ(表示しない)
  --headless-width <headless-width>              レンダリングする幅(文字数)
  --headless-height <headless-height>            レンダリングする高さ(文字数)
  --benchmark                                    全フレーム全速処理して、終了時に処理時間の内訳を表示
  --no-capture-border                            黄色い枠線を出さない(薄気味悪い)
  --no-window-frame                              クライアント領域のみキャプチャー
  --list-window                                  ウインドウリスト表示
  --list-monitor                                 モニターリスト表示
  -?, -h, --help                                 Show help and usage information
  --version
  ```