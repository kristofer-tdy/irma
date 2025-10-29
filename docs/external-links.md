# External Links

This document contains a curated list of external links that have inspired or are expected to inspire the implementation of Irma. These resources are valuable for both human developers and AI Agents working on this project.

To add a new link, please add a new section with a descriptive title, the link itself, a brief explanation of its relevance, and a few bullet points highlighting key takeaways or areas of interest.

---

## Azure AI Foundry Baseline

- **Link:** `https://github.com/Azure-Samples/azure-ai-foundry-baseline`
- **Description:** This repository provides a baseline implementation for a web-based chat application using Azure AI Foundry as its backend. While it is a complete web application with a UI, several components and patterns can inform the development of Irma's WebApi.
- **Key Aspects:**
  - **General Architecture:** Provides a reference for a production-ready chat application, though it may be more complex than what Irma requires.
  - **Infrastructure as Code (IaC):** The Bicep templates are a good source of inspiration for provisioning Azure resources for Irma.
  - **Agent Definition:** Demonstrates how to define an agent's behavior and configuration using a JSON file, a pattern that could be adopted for Irma.

---

## Microsoft 365 Copilot Chat API Overview

- **Link:** `https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/api/ai-services/chat/overview`
- **Description:** This official Microsoft documentation outlines the API for interacting with Microsoft 365 Copilot. The design of Irma's REST API has been heavily influenced by this specification, aiming for a similar interaction model.
- **Key Aspects:**
    - **API Design:** Serves as the primary reference for Irma's API structure, endpoints, and data contracts.
    - **Interaction Model:** Understanding this API is key to understanding the intended request/response flow for Irma.
