# StreetArtHunter API

A serverless backend architecture built to collect, manage, and store crowdsourced street art photography and metadata. Designed with a focus on scalability, cloud-native patterns, robust observability, and secure, asynchronous processing.

## 📌 Overview

StreetArtHunter API is an event-driven, Azure Functions-based RESTful service. It allows users to submit images of street art along with relevant metadata (location, description). The system efficiently processes multipart form data, streams images to cloud storage, and utilizes a message broker to asynchronously save structured metadata into a highly scalable NoSQL database, ensuring rapid API response times.

## 🏗️ Architecture & Technologies

* **Framework:** .NET 10
* **Compute:** Azure Functions (Isolated Worker Model)
* **Storage:** Azure Blob Storage (for high-performance image streaming)
* **Message Broker:** Azure Service Bus (for asynchronous task queuing)
* **Database:** Azure Cosmos DB (NoSQL for low-latency metadata retrieval)
* **Security:** Azure Key Vault & Managed Identity (Passwordless architecture)
* **Observability:** Azure Application Insights
* **DevOps:** GitHub Actions (CI/CD)

## 🚀 Key Features

* **Multipart Form Uploads:** Securely accepts and processes `multipart/form-data` containing both binary files and textual metadata in a single HTTP request.
* **Cloud-Native Storage:** Automatically generates unique identifiers (GUIDs) for images, sets correct HTTP headers (`ContentType`), and streams data directly to Azure Blob Storage.
* **Event-Driven & Asynchronous Processing:** Decouples the HTTP upload process from database writes. The API quickly returns a `202 Accepted` status while dropping a payload into an
* **Azure Service Bus** queue. A background worker function (`ServiceBusTrigger`) reliably picks up the message and writes it to Cosmos DB, providing built-in retry mechanisms and dead-lettering.
* **Zero-Trust Security:** Implements Azure Key Vault with System-Assigned Managed Identity. All connection strings (Service Bus, Cosmos DB, Storage) are stored securely as secrets and injected into the Function App via Key Vault References, completely eliminating hardcoded credentials.
* **Distributed Tracing & Telemetry:** Fully instrumented with Application Insights to provide end-to-end distributed tracing across the Function app, Blob Storage, Service Bus, and Cosmos DB. Custom logging filters are implemented in `host.json` to bypass default production sampling and ensure critical business logs are captured.
* **Automated Deployments:** Connected to a GitHub Actions CI/CD pipeline using ZipDeploy (Run From Package) for immutable, consistent releases utilizing Federated Credentials (`GitHubActionsUser`).

## 🔌 API Usage

### Upload a new mural
**Endpoint:** `POST /api/murals`
**Authorization:** Anonymous (for demo purposes)
**Content-Type:** `multipart/form-data`

**Request Body (Form Data):**
* `image`: [File] (e.g., .jpg, .png)
* `description`: [Text] "A massive colorful piece spanning the entire side of a brick building."
* `location`: [Text] "Shoreditch, London"

**Successful Response (202 Accepted):**
```json
{
  "Message": "Awesome! We've received your mural. It will appear in the gallery shortly.",
  "MuralId": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
  "ImageUrl": "https://<your-storage-account>.blob.core.windows.net/murals/a1b2c3d4.jpg"
}
```
