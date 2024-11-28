from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, model_validator
import matplotlib.pyplot as plt
import io
import base64

# Initializing App
app = FastAPI()


# Basic Pydantic Instance to ensure data integrity
# Use it at my job all the time, in LOVE with this package
# It also keeps me up at night.
class ChartRequest(BaseModel):
    chart_type: str
    data: list[float]
    labels: list[str]
    title: str

    @model_validator(mode="after")  # To ensure validation happens after parsing
    def validate_chart_data(cls, model):
        # Very basic validations
        if not model.data:
            raise ValueError("Data cannot be empty")

        # Validation for specific chart types
        # Realized the validation process is slightly different for line charts
        if model.chart_type in ["bar", "pie"] and len(model.data) != len(model.labels):
            raise ValueError("Data and labels have mismatched lengths: Relevant for bar/pie charts.")
        if model.chart_type == "line" and model.labels and len(model.data) != len(model.labels):
            raise ValueError("For line charts, labels must match data length or be empty")

        return model


try:
    @app.post("/generate-chart")
    def generate_chart(request: ChartRequest):
        # Validate chart-checks (just some simple safety checks)
        if request.chart_type in ["bar", "pie"] and len(request.data) != len(request.labels):
            raise HTTPException(status_code=400, detail="Data and labels length mismatch for bar/pie chart")
        elif request.chart_type == "line" and len(request.data) != len(request.labels) and len(request.labels) > 0:
            raise HTTPException(status_code=400, detail="For a line chart, labels should match data or be empty")

        # Validate non-empty data
        if len(request.data) == 0:
            raise HTTPException(status_code=400, detail="Data cannot be empty")

        # Generate a chart
        plt.figure(figsize=(10, 8))
        if request.chart_type == "bar":
            plt.bar(request.labels, request.data)
        elif request.chart_type == "line":
            plt.plot(request.labels, request.data)
        elif request.chart_type == "pie":
            plt.pie(request.data, labels=request.labels, autopct='%1.1f%%')

        # Adding title
        plt.title(request.title)

        # Saving chart to memory
        buf = io.BytesIO()
        plt.savefig(buf, format="png")  # Requested Format by Finlay
        buf.seek(0)
        # Decode
        encoded_chart = base64.b64encode(buf.read()).decode("utf-8")
        buf.close()
        plt.close()

        return {"chart": encoded_chart}
except Exception as e:
    raise HTTPException(status_code=500, detail=f"Error creating chart: {str(e)}.")
