"""Run the selected weekly segment models on a supplied sales history."""

import json
import math
import sys
from importlib.metadata import version
from pathlib import Path

import joblib
import numpy as np
import pandas as pd
import xgboost as xgb
from statsmodels.tsa.statespace.sarimax import SARIMAX
from threadpoolctl import threadpool_limits


ROOT = Path(__file__).resolve().parent
REGISTRY = json.loads((ROOT / "model_registry.json").read_text(encoding="utf-8"))


def verify_runtime():
    versions = json.loads((ROOT / "runtime_versions.json").read_text(encoding="utf-8"))
    for package in ("scikit-learn", "xgboost", "statsmodels", "numpy", "pandas", "joblib", "threadpoolctl"):
        if version(package) != versions[package]:
            raise RuntimeError(f"{package}=={versions[package]} is required")


def croston(history, alpha):
    size = None
    interval = None
    gap = 0
    for quantity in history.dropna():
        gap += 1
        if quantity > 0:
            size = float(quantity) if size is None else alpha * quantity + (1 - alpha) * size
            interval = float(gap) if interval is None else alpha * gap + (1 - alpha) * interval
            gap = 0
    return 0.0 if size is None else size / max(interval, 1.0)


def predict(entry, history, week):
    path = ROOT / entry["artifact"]
    artifact_format = entry["artifact_format"]
    if artifact_format in ("joblib_pipeline", "xgboost_ubjson"):
        features = {f"lag_{lag}": history.iloc[-lag] for lag in (1, 2, 4, 52)}
        features.update(
            roll4_mean=history.rolling(4, min_periods=2).mean().iloc[-1],
            roll13_mean=history.rolling(13, min_periods=6).mean().iloc[-1],
            month=week.month,
            weekofyear=int(week.isocalendar().week),
        )
        row = pd.DataFrame([features], columns=entry["features"])
        if artifact_format == "xgboost_ubjson":
            booster = xgb.Booster(params={"nthread": 2})
            booster.load_model(path)
            values = row.to_numpy()
            imputed = np.where(np.isnan(values), entry["imputer_statistics"], values)
            return float(booster.inplace_predict(imputed)[0])
        return float(joblib.load(path).predict(row)[0])

    artifact = json.loads(path.read_text(encoding="utf-8"))
    if artifact_format == "sarima_parameters_and_history_json":
        fitted = SARIMAX(
            history,
            order=tuple(artifact["order"]),
            seasonal_order=tuple(artifact["seasonal_order"]),
            enforce_stationarity=artifact["enforce_stationarity"],
            enforce_invertibility=artifact["enforce_invertibility"],
        ).filter(np.asarray(artifact["params"], dtype=float))
        return float(fitted.forecast(steps=1).iloc[0])

    if artifact_format == "history_json" and entry["model"] == "Croston":
        return croston(history, artifact["configuration"]["params"]["alpha"])

    raise ValueError(f"Unsupported model {entry['model']}")


def forecast(request):
    week = pd.Timestamp(request["forecastWeek"])
    weeks = pd.date_range(request["startWeek"], week - pd.Timedelta(weeks=1), freq="W-MON")
    if week.dayofweek != 0 or len(weeks) < 52:
        raise ValueError("At least 52 calendar weeks ending before a Monday are required")

    results = {}
    with threadpool_limits(limits=2):
        for segment, values in request["history"].items():
            try:
                entry = REGISTRY["segments"][segment]
                history = pd.Series(values, index=weeks, dtype=float)
                observations = history.dropna()
                if len(observations) < 52 or (observations < 0).any() or not np.isfinite(observations).all():
                    continue
                prediction = predict(entry, history, week)
                if math.isfinite(prediction):
                    results[segment] = {"model": entry["model"], "quantity": max(0.0, prediction)}
            except Exception as error:
                print(f"{segment}: {error}", file=sys.stderr)
    return results


if __name__ == "__main__":
    verify_runtime()
    json.dump(forecast(json.load(sys.stdin)), sys.stdout, allow_nan=False)
