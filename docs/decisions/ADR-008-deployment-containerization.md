# ADR-008: Deployment & Containerization Architecture

* **Status**: ACCEPTED
* **Deciders**: Solution Architect, DevOps / Infrastructure Team, Engineering Team
* **Date**: 2026-09-17
* **Technical Domain**: Cloud Architecture, AWS, Containerization, Infrastructure, CI/CD, Local Development

---

## Context and Problem Statement

Carpediem requires a cloud deployment topology on Amazon Web Services (AWS) and an aligned local development environment. The platform consists of:
1. **Frontend**: Angular 22 Single-Page Application (SPA) utilizing static compilation and client-side routing.
2. **Backend**: ASP.NET Core 10 Modular Monolith exposing standard REST endpoints.
3. **Database**: Microsoft SQL Server 2025 relational data store.
4. **Security Perimeter**: Same-origin secure `HttpOnly` cookie-based authentication (ADR-002), requiring the SPA and API to be exposed under a single canonical web origin.

We must define:
- The production cloud infrastructure on AWS (CloudFront, S3, ALB, ECS Fargate, RDS for SQL Server) enforcing network segmentation, least-privilege security, and edge routing.
- Caching strategies for immutable hashed assets vs. un-cached dynamic API responses.
- Safe, production-ready database migration execution aligning with ADR-009.
- A local containerized development topology that approximates production service relationships without introducing unnecessary simulation overhead.

---

## Decision Drivers

1. **Unified Origin Routing (ADR-002 Compliance)**: The production topology must serve both static frontend assets and dynamic `/api/*` endpoints under a single domain origin (e.g., `https://app.carpediem.org`), guaranteeing first-party cookie semantics and eliminating CORS preflights.
2. **Network Security & Subnet Isolation**: Backend application containers and databases must never be directly exposed to the public internet.
3. **Operational Simplicity & Manageability**: For a focused organization (~150 users, 2–20 events/month), container orchestration must avoid the heavy operational burden of Kubernetes (EKS), service meshes, or distributed microservices.
4. **Deterministic, Zero-Downtime Deployments**: Deployments must support rolling updates or blue/green transitions with automated rollbacks and safe, out-of-process database schema migrations (ADR-009).
5. **Local Development Parity**: Developers must be able to run and test the complete system locally with minimal friction, verifying same-origin routing and SQL Server persistence before pushing code.

---

## Considered Options

* **Option 1: Kubernetes (AWS EKS) with Ingress Controller**
  Deploy containerized frontend and backend pods into an Amazon EKS cluster with Istio or Nginx ingress.
* **Option 2: CloudFront + S3 (Static Frontend) + ALB + ECS/Fargate (API Container) + RDS for SQL Server (Chosen)**
  Serverless edge distribution via Amazon CloudFront path routing: static Angular assets served from a private S3 bucket; `/api/*` forwarded to an Application Load Balancer targeting serverless AWS ECS/Fargate containers; data persisted in private AWS RDS for SQL Server.
* **Option 3: Single Monolithic Container (Serving SPA from ASP.NET Core `UseStaticFiles`)**
  Package compiled Angular static files directly inside the ASP.NET Core Docker image and serve everything from ECS Fargate.

---

## Decision Outcome

**Chosen Option**: **Option 2: CloudFront + S3 + ALB + ECS/Fargate + RDS for SQL Server**.

### Production Architecture & Cloud Topology

```
                              [ Internet User ]
                                     │
                                     ▼ HTTPS (Port 443)
                         ┌───────────────────────┐
                         │   Route 53 DNS        │
                         │   (app.carpediem.org) │
                         └───────────┬───────────┘
                                     │
                                     ▼
                         ┌───────────────────────┐
                         │   AWS CloudFront      │
                         │   (ACM TLS Cert)      │
                         └─────┬───────────┬─────┘
           Default Path (/*)   │           │ Path (/api/*)
           (Static SPA Assets) │           │ (Dynamic API Requests)
                               ▼           ▼
┌────────────────────────────────┐       ┌─────────────────────────────────────────────────────┐
│  Private Amazon S3 Bucket      │       │  Public Subnets (VPC)                               │
│  - Static Angular 22 Artifacts │       │  ┌───────────────────────────────────────────────┐  │
│  - Origin Access Control (OAC) │       │  │  Application Load Balancer (ALB)              │  │
│  - Private (No Public Bucket)  │       │  │  - Terminates TLS (ACM Cert)                  │  │
└────────────────────────────────┘       │  │  - Forwards Cookies, CSRF, & Auth Headers     │  │
                                         │  └───────────────────────┬───────────────────────┘  │
                                         └──────────────────────────┼──────────────────────────┘
                                                                    │ Forward to Target Group
                                                                    ▼
                                         ┌─────────────────────────────────────────────────────┐
                                         │  Private Application Subnets                        │
                                         │  ┌───────────────────────────────────────────────┐  │
                                         │  │  AWS ECS on AWS Fargate                       │  │
                                         │  │  - ASP.NET Core 10 Container Tasks            │  │
                                         │  │  - awsvpc networking mode                     │  │
                                         │  │  - Health Check Endpoint: /api/health         │  │
                                         │  │  - Task Role: Secrets Manager & CloudWatch    │  │
                                         │  └───────────────────────┬───────────────────────┘  │
                                         └──────────────────────────┼──────────────────────────┘
                                                                    │ SQL Server Port 1433
                                                                    │ (Restricted Security Group)
                                                                    ▼
                                         ┌─────────────────────────────────────────────────────┐
                                         │  Isolated Database Subnets (No Internet / No NAT)   │
                                         │  ┌───────────────────────────────────────────────┐  │
                                         │  │  Amazon RDS for SQL Server 2025               │  │
                                         │  │  - Primary DB Instance (Multi-AZ Optional)    │  │
                                         │  │  - Automated Backups & KMS Encryption         │  │
                                         │  └───────────────────────────────────────────────┘  │
                                         └─────────────────────────────────────────────────────┘
```

---

### Key Infrastructure & Configuration Specifications

#### 1. Routing, DNS & SSL/TLS (Route 53 & ACM)
- **Domain**: `app.carpediem.org` managed via Amazon Route 53 with an alias record pointing to the CloudFront distribution.
- **TLS Termination**:
  - CloudFront terminates client TLS using an AWS Certificate Manager (ACM) certificate issued in `us-east-1` (mandatory for CloudFront).
  - ALB terminates edge-to-origin TLS using an ACM certificate issued in the workload region (e.g., `eu-west-1` or `us-east-2`).
  - Connections between CloudFront and ALB use HTTPS only (`OriginProtocolPolicy: https-only`).

#### 2. CloudFront Behavior, Caching Policies & Scoped Routing
- **Origin 1: Static Frontend (`/*`) $\rightarrow$ S3**:
  - S3 bucket is strictly private; public access is blocked completely.
  - CloudFront accesses S3 using **Origin Access Control (OAC)**.
  - **Asset Caching Policy**:
    - Compiled Angular hashed files (`/assets/*`, `*.js`, `*.css`, `*.woff2`, images) $\rightarrow$ `Cache-Control: public, max-age=31536000, immutable` via CloudFront `Managed-CachingOptimized` policy.
    - Root entry document (`/index.html`) $\rightarrow$ `Cache-Control: no-cache, no-store, must-revalidate` (short revalidation policy ensuring users immediately fetch newly deployed Angular bundles upon deployment).
  - **Scoped SPA Deep-Link Routing (No Global Error Masking)**:
    - *Anti-Pattern Prohibited*: Global CloudFront custom error response rules (e.g., mapping 403 or 404 to `/index.html` with HTTP 200 OK across the entire distribution) are **strictly forbidden**. Global error rewrites intercept API errors: if an endpoint under `/api/*` returns `404 Not Found`, `401 Unauthorized`, `403 Forbidden`, or `500 Internal Server Error`, a global rule would overwrite the status to 200 OK and return HTML markup, breaking Angular HTTP client error handling and masking failures.
    - *Scoped Viewer-Request Rewrite*: SPA client-side deep linking is handled strictly on the default S3 behavior (`/*`) via a lightweight edge function (e.g., CloudFront Function on viewer-request). If the requested path does not contain a file extension (e.g., `/events`, `/admin/staffing`, `/profile`), the function internally rewrites the request URI to `/index.html` before S3 evaluation while preserving the browser's address bar.
    - If a specific static file key is genuinely missing from S3 (e.g., `/assets/missing.png`), S3 returns 404 cleanly.
- **Origin 2: Dynamic API (`/api/*`) $\rightarrow$ ALB**:
  - **Cache Policy**: `Managed-CachingDisabled` (`Cache-Control: no-store, no-cache, max-age=0`). Dynamic API responses are never cached by CloudFront.
  - **Origin Request Policy**: `Managed-AllViewerExceptHostHeader` (or custom equivalent). Forwards all viewer request headers (specifically `Cookie`, `Authorization`, `X-CSRF-TOKEN` / `X-XSRF-TOKEN`, and `Content-Type`), query strings, and request bodies to the ALB, ensuring strict compliance with ADR-002 cookie authentication.
  - **API Error Pass-Through**: All backend HTTP error responses (400, 401, 403, 404, 409, 422, 500) pass through transparently to the client with their original status codes, headers, and JSON error payloads.

#### 3. Network Segmentation & Security Groups
- **VPC Topology**:
  - **Public Subnets**: Host the internet-facing Application Load Balancer and NAT Gateways across multiple Availability Zones.
  - **Private Application Subnets**: Host ECS Fargate container tasks. Inbound traffic is allowed strictly from the ALB security group on the container port (e.g., port 8080). Outbound internet access is routed via NAT Gateway (for pulling ECR images, CloudWatch logs, and AWS Secrets Manager).
  - **Isolated Database Subnets**: Host Amazon RDS for SQL Server 2025. Zero internet access; zero NAT routing. Inbound traffic on port 1433 is restricted strictly to the ECS task security group.
- **ALB Origin Protection (Preventing Direct CloudFront Bypass)**:
  - The ALB is shielded from direct public internet bypass to prevent clients from circumventing CloudFront security policies, WAF rules, or rate limits:
    1. **Custom Header Verification**: CloudFront attaches a secret header (e.g., `X-Origin-Verify: <cryptographic-token>` managed via AWS Secrets Manager) to all origin requests directed to the ALB. The ALB listener rules evaluate this header and immediately return `403 Forbidden` for any request lacking the valid token.
    2. **CloudFront Managed Prefix List**: The ALB security group restricts inbound HTTPS (port 443) traffic strictly to IP addresses belonging to the AWS CloudFront Origin-Facing Managed Prefix List (`com.amazonaws.global.cloudfront.origin-facing`).
    3. Direct public internet access to the ALB hostname is blocked.

#### 4. Configuration & Secrets Management
- **Non-Sensitive Settings**: Injected via AWS Systems Manager Parameter Store into ECS task definition environment variables (e.g., `ASPNETCORE_ENVIRONMENT=Production`, `Logging__LogLevel__Default=Information`).
- **Sensitive Secrets**: Stored in AWS Secrets Manager (e.g., database connection string, data protection keys) and referenced directly in the ECS task definition using IAM task execution roles (`secrets: [ { name: "ConnectionStrings__CarpediemDb", valueFrom: "arn:aws:secretsmanager:..." } ]`).
- Secrets are never committed to source control or baked into Docker container images.

#### 5. Container Lifecycle & Health Checks
- **Health Check Endpoint**: The ASP.NET Core API exposes a dedicated health check endpoint at `GET /api/health` using `Microsoft.Extensions.Diagnostics.HealthChecks`.
- **Readiness Probe**: Verifies internal application responsiveness and database connectivity before the ALB routes live traffic to a newly launched container task.
- **Graceful Shutdown**: The container listens for `SIGTERM` signals from ECS, giving in-flight HTTP requests up to 30 seconds to drain before stopping.

#### 6. Database Schema Migrations (ADR-009 Integration)
- In compliance with ADR-009, containers **never** execute `context.Database.Migrate()` on application startup. Automatic migration execution at container boot can cause locking contention, race conditions across multiple Fargate replicas, or deployment failures.
- **Migration Pipeline**:
  - CI/CD builds generate an idempotent SQL script (`dotnet ef migrations script --idempotent`).
  - Schema migrations are executed out-of-process via an automated CI/CD deployment step (e.g., an AWS CodeBuild / GitHub Actions runner within the VPC or an ephemeral ECS migration task) prior to cutting over traffic to the new API container task version.

---

### Local Development Topology

Local development approximates the production service topology without attempting to replicate cloud-specific managed services:
1. **Angular SPA**: Developers run the Angular development server locally (`ng serve` on `http://localhost:4200`).
2. **Same-Origin Emulation**:
   - The Angular dev server uses `proxy.conf.json` to proxy `/api/*` requests directly to the local ASP.NET Core backend (`https://localhost:5001`).
   - This replicates first-party same-origin cookie behavior on local workstations without needing CORS configurations.
3. **Local Database**: Microsoft SQL Server 2025 runs locally inside Docker (`mcr.microsoft.com/mssql/server:2025-latest`).
4. **Docker Compose Topology**:
   - A `docker-compose.yml` file is provided for running the integrated stack locally:
     - `carpediem-db`: SQL Server 2025 container.
     - `carpediem-api`: ASP.NET Core 10 backend container.
     - `carpediem-proxy`: Lightweight reverse proxy (Nginx or YARP) routing `/api` to the backend and `/` to the frontend, mirroring the CloudFront/ALB path split.
   - *Parity Disclaimer*: Local Docker Compose approximates the application, API, and database service topology. CloudFront edge distribution, AWS IAM roles, ACM certificates, and RDS managed maintenance are production-specific AWS services and are not simulated locally.

---

## Pros and Cons of the Options

### Option 2: CloudFront + S3 + ALB + ECS Fargate + RDS SQL Server (Chosen)
* **Good**: Native, high-performance static SPA delivery via CloudFront edge caching with minimal bandwidth cost.
* **Good**: Serverless backend compute via AWS Fargate eliminates server patching, OS maintenance, and cluster scaling overhead.
* **Good**: Strict compliance with ADR-002: CloudFront path routing provides clean same-origin cookie semantics with zero CORS preflight overhead.
* **Good**: Multi-layer network security: database and containers reside in private, isolated subnets.
* **Good**: Far lower operational complexity and cost than running an Amazon EKS (Kubernetes) cluster for ~150 users.
* **Bad**: Requires configuring two origin behaviors in CloudFront and managing separate S3 deployment and ECS deployment pipelines in CI/CD.

### Option 1: Kubernetes (AWS EKS)
* **Good**: Highly standardized enterprise container orchestration platform.
* **Bad**: Massive operational and cost overhead: managing control planes, node groups, ingress controllers, cert-managers, and YAML manifests is unjustifiable for a single modular monolith.

### Option 3: Single Monolithic Container (Serving SPA from ASP.NET Core)
* **Good**: Single build artifact and deployment target.
* **Bad**: Every static file request (images, JS chunks, CSS) consumes backend container compute and memory, bypassing CloudFront edge benefits.
* **Bad**: Frontend UI bug fixes require rebuilding, testing, and deploying the entire backend container and restarting application tasks.

---

## Security & Reliability Impact

1. **Defense-in-Depth**:
   - CloudFront provides native DDoS protection (AWS Shield Standard).
   - Inbound HTTP access to ALB is encrypted over TLS 1.3.
   - Database is completely unreachable from the public internet.
2. **Data Protection at Rest & in Transit**:
   - RDS for SQL Server utilizes AWS KMS encryption at rest.
   - Connections between ECS tasks and SQL Server require encrypted TLS (`Encrypt=True;TrustServerCertificate=False`).
3. **Observability**: Structured JSON logs stream to Amazon CloudWatch Logs with AWS Container Insights enabled for container CPU/memory tracking.

---

## Revisit Triggers

1. If the organization expands into multi-region deployments requiring geo-located application container clusters outside a single AWS region.
2. If Carpediem's scale grows by orders of magnitude, justifying extraction of specific services or adoption of advanced Kubernetes orchestration.
