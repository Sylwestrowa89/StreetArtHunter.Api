# StreetArtHunter API

A serverless backend architecture built to collect, manage, and store crowdsourced street art photography and metadata. Designed with a focus on scalability, cloud-native patterns, and robust observability.

## 📌 Overview

StreetArtHunter API is an Azure Functions-based RESTful service. It allows users to submit images of street art along with relevant metadata (location, description). The system efficiently processes multipart form data, streams images to cloud storage, and saves structured metadata into a highly scalable NoSQL database.

## 🏗️ Architecture & Technologies

* **Framework:** .NET 10
* **Compute:** Azure Functions (Isolated Worker Model)
* **Storage:** Azure Blob Storage (for high-performance image streaming)
* **Database:** Azure Cosmos DB (NoSQL for low-latency metadata retrieval)
* **Observability:** Azure Application Insights
* **DevOps:** GitHub Actions (CI/CD)

## 🚀 Key Features

* **Multipart Form Uploads:** Securely accepts and processes `multipart/form-data` containing both binary files and textual metadata in a single HTTP request.
* **Cloud-Native Storage:** Automatically generates unique identifiers (GUIDs) for images, sets correct HTTP headers (`ContentType`), and streams data directly to Azure Blob Storage.
* **Distributed Tracing & Telemetry:** Fully instrumented with Application Insights to provide end-to-end distributed tracing across the Function app, Blob Storage, and Cosmos DB. Custom logging filters are implemented in `host.json` to bypass default production sampling and ensure critical business logs are captured.
* **Automated Deployments:** Connected to a GitHub Actions CI/CD pipeline using ZipDeploy (Run From Package) for immutable, consistent releases.

## 🔌 API Usage

### Upload a new mural

**Endpoint:** `POST /api/murals`  
**Authorization:** Anonymous (for demo purposes)  
**Content-Type:** `multipart/form-data`

**Request Body (Form Data):**
* `image`: [File] (e.g., .jpg, .png)
* `description`: [Text] "A massive colorful piece spanning the entire side of a brick building."
* `location`: [Text] "Shoreditch, London"

**Successful Response (200 OK):**
```json
{
  "message": "Success! Image saved AND database entry added.",
  "muralId": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
  "imageUrl": "https://<your-storage-account>.blob.core.windows.net/murals/a1b2c3d4.jpg"
}
```