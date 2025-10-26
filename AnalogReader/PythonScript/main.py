import cv2
import os
import numpy as np
import math
from ultralytics import YOLO
from datetime import datetime
import json
import traceback
import sys

os.environ["ULTRALYTICS_VERBOSITY"] = "0"
os.environ["YOLO_VERBOSE"] = "false"

# 指針の範囲
MIN_ANGLE = 129.42
MAX_ANGLE = 49.23
# 電圧の範囲 
MIN_VOLTAGE = 0.0
MAX_VOLTAGE = 3.0
# 軸を計算するためのオフセット値
OFF_SET = 120
# YOLO導入
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
MODEL_PATH = os.path.join(SCRIPT_DIR, "best.pt")


DEBUG_DIR = os.path.join(SCRIPT_DIR, "debug_out")
os.makedirs(DEBUG_DIR, exist_ok=True)
def dpath(name: str) -> str:
    """debug_out 直下の絶対パスを返す。"""
    return os.path.join(DEBUG_DIR, name)

def drel(name: str) -> str:
    """スクリプト基準の相対パス（debug_out/～）を返す。"""
    return os.path.join("debug_out", name)

def _order_points_tl_tr_br_bl(pts: np.ndarray) -> np.ndarray:
    """4点を TL, TR, BR, BL 順に並べ替える"""
    # pts: shape (4, 2)
    rect = np.zeros((4, 2), dtype="float32")

    # 左上は (x+y) が最小、右下は最大
    s = pts.sum(axis=1)
    rect[0] = pts[np.argmin(s)]   # TL
    rect[2] = pts[np.argmax(s)]   # BR

    # 残り2点は差分(y-x)で判定：最小がTR、最大がBL
    diff = np.diff(pts, axis=1)   # (y - x)
    rect[1] = pts[np.argmin(diff)]  # TR
    rect[3] = pts[np.argmax(diff)]  # BL
    return rect

def crop_with_highest_confidence(image, model):
    """YOLO セグメンテーションで最も信頼度の高いマスク領域を台形変換で切り出す。

    Args:
        image: 入力 BGR 画像（OpenCV）。
        model: Ultralytics YOLO モデル（segment対応）。

    Returns:
        切り出し画像

    Raises:
        ValueError: マスク/ボックスが検出できない場合。
    """

    results = model.predict(image, task="segment", conf=0.25, iou=0.7, verbose=False)

    if (not results) or (results[0].masks is None) or (results[0].boxes is None):
        raise ValueError("YOLOでマスクが検出されませんでした")

    # 信頼度最大の index を取得
    confs = results[0].boxes.conf.cpu().numpy()
    best_idx = int(np.argmax(confs))

    # 可視化（半透明オーバーレイ）
    m = results[0].masks.data[best_idx].cpu().numpy()  # (H, W), 0/1
    if m.dtype != np.uint8:
        m = (m * 255).astype(np.uint8)

    ih, iw = image.shape[:2]
    if m.shape != (ih, iw):
        m = cv2.resize(m, (iw, ih), interpolation=cv2.INTER_NEAREST)

    overlay_img = image.copy()
    color = np.array([0, 255, 0], dtype=np.uint8)
    mask_bool = m > 0
    overlay_img[mask_bool] = (0.65 * overlay_img[mask_bool] + 0.35 * color).astype(np.uint8)

    mask = results[0].masks.xy[best_idx]
    pts = np.array(mask, dtype=np.float32)

    # 回転付き外接短形
    rect = cv2.minAreaRect(pts)
    # 4点取得
    box  = cv2.boxPoints(rect).astype(np.float32)
    # 4点を TL, TR, BR, BL 順に並べ替える
    ordered = _order_points_tl_tr_br_bl(box)

    # 対辺同士（0-1と2-3, 1-2と3-0）の距離を比較して最大値を採用
    width  = int(max(np.linalg.norm(ordered[0] - ordered[1]), np.linalg.norm(ordered[2] - ordered[3])))
    height = int(max(np.linalg.norm(ordered[1] - ordered[2]), np.linalg.norm(ordered[3] - ordered[0])))
    # 幅・高さが 0 にならないように最低1を保証
    width  = max(width, 1)
    height = max(height, 1)

    # 透視変換後の出力画像の四隅座標（左上→右上→右下→左下）
    dst_pts = np.array([
        [0, 0],
        [width - 1, 0],
        [width - 1, height - 1],
        [0, height - 1]
    ], dtype=np.float32)
    # 変換行列を計算（元の矩形→正規化矩形）
    M = cv2.getPerspectiveTransform(ordered, dst_pts)
    # 射影変換を適用して矩形を切り出し
    warped = cv2.warpPerspective(image, M, (width, height))

    return warped

# 読み取りの計算
def measure_voltage(image):
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    annot_rel_path = ""

    model = YOLO(MODEL_PATH)
    img = crop_with_highest_confidence(image, model)

    # 640*640にリサイズ
    target_size = 640
    img = cv2.resize(img, (target_size, target_size), interpolation=cv2.INTER_AREA)
    
    # HSV色空間に変換
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)

    # 赤い範囲の定義
    lower_red1 = np.array([0, 43, 46])
    upper_red1 = np.array([10, 255, 255])
    lower_red2 = np.array([156, 43, 46])
    upper_red2 = np.array([180, 255, 255])

    # マスクを作る
    mask1 = cv2.inRange(hsv, lower_red1, upper_red1)
    mask2 = cv2.inRange(hsv, lower_red2, upper_red2)

    # マスクをマージする
    red_mask = cv2.bitwise_or(mask1, mask2)
    #cv2.imshow("Red Mask", red_mask)

    region_mask = np.zeros_like(red_mask, dtype=np.uint8)

    cv2.rectangle(region_mask, (40, 40), (600, 300), 255, thickness=cv2.FILLED)
    masked_red = cv2.bitwise_and(red_mask, region_mask)
    #cv2.imshow("Region Masked Red", masked_red)
    #cv2.waitKey(0)
    #cv2.destroyAllWindows()
    red_mask = masked_red

    # すべてのマスク領域を特定する
    contours, _ = cv2.findContours(red_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    if contours:
        # 一番大きなマスク領域を探す
        largest_contour = max(contours, key=cv2.contourArea)

        # 一番大きなマスク領域のみを抽出する
        filtered_mask = np.zeros_like(red_mask)
        cv2.drawContours(filtered_mask, [largest_contour], -1, 255, thickness=cv2.FILLED)

        # マスク内の座標を抽出する
        ys, xs = np.where(filtered_mask > 0)
        points = np.column_stack((xs, ys)).astype(np.float32)


        #cv2.imshow("Filtered Mask", filtered_mask)

        if len(points) > 0:
            # 直線フィッティング
            [vx, vy, x0, y0] = cv2.fitLine(points, cv2.DIST_L2, 0, 0.01, 0.01)

            # 線を描く
            length = 200
            x1 = int(x0[0] - vx[0] * length)
            y1 = int(y0[0] - vy[0] * length)
            x2 = int(x0[0] + vx[0] * length)
            y2 = int(y0[0] + vy[0] * length)

        else:
            print("座標抽出が失敗した")
    else:
        print("マスク領域が特定できない")

    # 電圧計の軸を特定する
    h, w, _ = img.shape
    center = (w // 2, h // 2 + OFF_SET)

    # 指針の端点を特定する
    dist1 = np.hypot(x1 - center[0], y1 - center[1])
    dist2 = np.hypot(x2 - center[0], y2 - center[1])
    tip = (x1, y1) if dist1 > dist2 else (x2, y2)

    # C#に渡す用に描画・確認
    debug_line = img.copy()
    cv2.line(debug_line, (x1, y1), (x2, y2), (0, 0, 255), 2) # 指针：红色

    annot_rel_path = drel(f"annotated_{ts}.jpg")
    cv2.imwrite(dpath(os.path.basename(annot_rel_path)), debug_line)

    # 角度を計算する
    dx = tip[0] - center[0]
    dy = center[1] - tip[1]
    angle = math.degrees(math.atan2(dy, dx))

    # 角度によって電圧計を推定する
    voltage = (angle - MIN_ANGLE) / (MAX_ANGLE - MIN_ANGLE) * (MAX_VOLTAGE - MIN_VOLTAGE)
    voltage = max(min(voltage, MAX_VOLTAGE), MIN_VOLTAGE)
    voltage = round(voltage, 2) 

    # === 結果を標準出力 ===
    return {
        "angle_deg": float(angle),
        "value": float(voltage),
        "confidence": 0.95,
        "annotated_path": annot_rel_path
        }

def main():
    """C# からの JSON 引数を受け取り、推定結果を標準出力（JSON）に返す。

    戻り値（プロセス終了コード）:
        0: 正常終了
        1: 例外などの一般エラー
        2: 引数不足
        3: 画像未発見
    """

    try:
        if len(sys.argv) < 2:
            print(json.dumps({"error": "no json arg"}))
            return 2

        req = json.loads(sys.argv[1])
        image_path      = req.get("imagePath")

        if not image_path or not os.path.exists(image_path):
            print(json.dumps({"error": f"image not found: {image_path}"}))
            return 3

        image = cv2.imread(image_path)
        result = measure_voltage(image)

        # ここで初めてstdoutにJSONを出す
        print(json.dumps(result, ensure_ascii=False))
        # エラー辞書なら非0終了
        return 1 if isinstance(result, dict) and "error" in result else 0

    except Exception as e:
        print(json.dumps({"error": str(e), "trace": traceback.format_exc()}))
        return 1

if __name__ == "__main__":
    # オプション: 起動確認
    with open(os.path.join(SCRIPT_DIR, "RUNNING.txt"), "w", encoding="utf-8") as f:
        f.write("started\n")
    sys.exit(main())