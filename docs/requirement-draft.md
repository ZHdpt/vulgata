# Project Vulgata Requirement Draft

This draft describe a vague for the project, refined requirements are expected in the future.

## Project Vulgata
Extract business logic from source code cross systems and repositories.

## Background Info
In a large company like a bank in our cases, many systems are running, and each system has multiple repositories.
These repositories contain source code, which is written in different languages, and have different architectures.
It is difficult to understand the business logic from these source codes. Even for non-IT guys who admin the system or design
how business logic is implemented, they may not understand the business logic clearly. Our goal is to create a system base on LLM,
which can help non-IT guys to understand the business logic from source codes.
There have been tools to generate wiki from single repository, but they are not very useful for our case.
We want to create a system, which can help non-IT guys to understand the business logic from source codes cross systems and repositories, 
escpecially in cases of cross-system or cross-repository.

## Features

- A web interface prociding user interaction.
    - basic website feature including login, logout, register, user profile, user management, authorization, and authentication.
    - a dashboard for system management, including project creation, deletion, and management.
    - repository management, including repository creation, deletion, and management to a system.
    - user can add suppliment info to a system or repository, which helping LLM to understand the context, including uploading files.
    - a system contains multiple repositories
    - a repository can link to a git repository.
    - user can start scanning a system or a single repository.
    - during scanning, user can view the progress and status, including how many agents are running, how many files are processed, and how many errors are occurred.
    - while scanning, LLM may ask questions about user, user can see it as a notification, and can answer it.
    - the system can monitor git repository, when current master is behind the remote, it will pull changes and relaunch scanning if there is no scanning in progress.
- A web chat interface, providing user interaction with LLM on systems or other knowledge.
    - user can ask questions to LLM, and get answers.
    - user can upload files to LLM, and get results.
    - user can manually specify which systems or repositories to use.

---

## How scanning work

My proposal is to create a human in loop system with multiple agents running together for one repository.
Agents scan the repository from file to files.
One agent is set as orchestrator, which is responsible for managing the scanning process, including assigning tasks to other agents, monitoring the progress, and handling errors.
For each code unit, like a function, a method, a class, or a file, an agent will read the code, and try to understand the business logic behind it which put in a document with stuctured metadata linked to the code unit. The generated document is organized in the same tree structure as the source code.
Documents that can directly linked to code units are called "code-linked documents", and documents that cannot directly linked to code units but can be linked to other documents are called "non-code-linked documents". For example, a document describing the overall architecture of the system is a non-code-linked document, but it can be linked to code-linked documents describing the business logic of specific code units. 
Documents can be classified into two types: "code logic document", "business logic document". Obsiously, all code-linked documents are code logic documents, but non-code-linked documents can be either code logic documents or business logic documents. For example, a document describing the overall architecture of the system is a non-code-linked document, but it is a code logic document because it describes the code structure and organization. On the other hand, a document describing the business process of the system is a non-code-linked document, but it is a business logic document because it describes the business logic of the system which are built on top of the code logic document.
Sometimes, a doucment may contain uncertain thing which LLM is not sure about when it comes to interact with other system, calling methods from other repositories or libraries, or quering databases. In this case, a meta data field called "uncertainty" can be added to the document, including the type of uncertainty, the reason for uncertainty, and submit to a particular place.
A group of special agents are responsible for handling these uncertainties, which can be called "uncertainty agents". Uncertainty agents will collect all the uncertainties from documents, and try to resolve them by asking questions to users or other agents. The resolved uncertainty will be added to the corresponding document, and the document will be updated accordingly. If the uncertainty cannot be resolved, it will be marked as "unresolved", and user can view it in the dashboard, and try to resolve it manually.
The scanning process is an iterative process, which will continue until all the code units are processed, and all the uncertainties are resolved or marked as unresolved. During the scanning process, user can view the progress and status, including how many agents are running, how many files are processed, and how many errors are occurred. User can also see the questions asked by LLM, and answer them to help LLM understand the business logic better.
All human submitted information, should mark as "human input", when information from human input can be surly conclude from souce code, info directly from source code has higher priority.

When uncertains come from interaction with other systems, uncertainty agents should check if those system alreay been scanned, if they are, the wake a agent for that system to answer the questions. These resolved uncertainties should have a link to the corresponding document in the other system, which can be used for traceability and maintenance. If those systems have not been scanned, uncertainty agents should ask user to provide the information, and mark it as "human input". If the target system is under an active scanning, pospone the question until the scanning is finished, the scanning for source system can be set as waiting for the scanning of target system, and will automatically start when the scanning of target system is finished. During waiting, the system can response to questions of uncertainties from other systems to avoid deadlocks.

During scanning, all code actions crossing system boundary should be recorded, including calling methods from other repositories or libraries.

When scanning is finished, all generated documents will be organized in a tree structure, which is the same as the source code structure. User can view the documents in the dashboard, and can click on the document to view the details. User can also search for specific documents by keywords, and filter documents by type, system, repository, or other metadata.



When an update hapens in a repository, the system will automatically pull the changes, and relaunch scanning if there is no scanning in progress. During scanning, the system will compare the new code with the old code, and only process the changed code units. All documents linked to the changed code units will be updated accordingly, and all uncertainties related to the changed code units will be re-evaluated. User can view the update history of each document, and can compare different versions of the document to see what has changed. The old documents will be archived, and can be accessed if being manually specified. 

Built-in database tools for LLM to query database. Users can append one or more database coneection to a repository, and LLM can use the database tools to inspect the database schema, and query the database to get the sample data. 

MCP tools customization is available in repo, system or global level.

---


