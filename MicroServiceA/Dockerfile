# This Microservice was built on Python 3.11.5
FROM python:3.11-slim

# Set working directory within container
WORKDIR /app

# Copy requirements.txt to container
COPY requirements.txt .

# Install dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy the app files to container
COPY . .

# Expose port the app runs on
EXPOSE 8000

# Running charts using fastAPI and uvicorn
CMD ["uvicorn", "charts:app", "--host", "0.0.0.0", "--port", "8000"]
