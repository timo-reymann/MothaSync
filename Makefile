.PHONY: help

SHELL := /bin/bash
VERSION?=$(shell git describe --tags --always)
COMMIT_REF=$(shell git rev-parse --short HEAD)
CONTAINER_REGISTRY?=docker.io/timoreymann
SERVER_IMAGE=$(CONTAINER_REGISTRY)/mothasync-server
COMPOSE=docker compose

clean: ## Remove all build artifacts
	@rm -rf dist/

help: ## Display this help page
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[33m%-30s\033[0m %s\n", $$1, $$2}'

build: ## Build the solution
	@dotnet build

test: ## Run all tests
	@dotnet test

build-release: ## Build the release archives for the client
	@./scripts/build.sh

publish-homepage: ## Publish the homepage for VERSION to the gh-pages branch
	@./scripts/publish-homepage.py "$(VERSION:v%=%)"

build-image-server: ## Build and push the mothasync-server container image
	@docker buildx build . \
		-t $(SERVER_IMAGE):$(VERSION) \
		-t $(SERVER_IMAGE):latest \
		-f docker/server.Dockerfile \
		--platform linux/amd64,linux/arm64 \
		--push

build-image: build-image-server ## Build all images

compose-up: ## Start the docker compose sample (server + prometheus)
	@$(COMPOSE) up -d

compose-down: ## Stop the docker compose sample
	@$(COMPOSE) down

run-server: ## Run the server locally without a container
	@dotnet run --project src/MothaSync.Server

run-client: ## Run the Avalonia client locally without a container
	@dotnet run --project src/MothaSync.Client
