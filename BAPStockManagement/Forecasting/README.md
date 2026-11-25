# Smart Stock AI dashboard

The web app passes weekly sales totals to the bundled Python worker through standard input. The worker has no database client. Install Python 3.13 and the pinned packages before running the dashboard:

```powershell
python -m venv obj\forecast-env
obj\forecast-env\Scripts\python.exe -m pip install -r Forecasting\requirements.txt
$env:SmartStockForecast__PythonExecutable = (Resolve-Path obj\forecast-env\Scripts\python.exe).Path
```

When running from the project directory, the web app can discover the virtual environment in `obj/forecast-env` automatically. For a published app, set `SmartStockForecast__PythonExecutable` to the absolute path of the installed environment's Python executable if Python is outside PATH. The forecast card reports worker failures separately from missing sales history.

- The weekly series uses the sale transaction type already used for billing. Quantities follow each product's selling unit. The training rule excludes individual sales lines above 100 units. Weeks start on Monday in Bangkok.
- Completed weeks in the database take precedence over the handoff history. The handoff series was extracted from the training CSV identified by `data_sha256` in `model_registry.json`. Database sales and product stock are read only.
- The date picker selects the Monday–Sunday week containing that date. Sales inputs end before the selected Monday. Dates before the model handoff cutoff and weeks beyond the current Bangkok week are unavailable. Historical stock analysis uses signed ledger movements through the Sunday before the selected week.
- SKU assignments come from the cleaned movement data where the assignment was unique. New or ambiguous SKUs use the current catalog category and bicycle size. Recommendations allocate segment demand by each SKU's sales share in the last 12 weeks. The default safety stock is five selling units per SKU with positive forecast demand, configurable as `SmartStockForecast__SafetyStock`.
- The XGBoost booster was exported to its portable UBJSON format from the supplied pipeline, with the original imputer statistics retained in the registry.
