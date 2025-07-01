#!/bin/bash

# Exit immediately if a command exits with a non-zero status.
set -e

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" &>/dev/null && pwd)
SOLUTION_DIR=$(realpath "$SCRIPT_DIR") # Assuming this script is in the root of the solution

DOCKER_IMAGE_NAME="wgmanager-integration-tests"
LIBRARY_PROJECT_PATH="WireGuardManager/WireGuardManager.csproj"
INTEGRATION_TEST_PROJECT_PATH="WireGuardManager.IntegrationTests/WireGuardManager.IntegrationTests.csproj"

# Output directories for published projects relative to SOLUTION_DIR
PUBLISH_LIB_DIR="publish_lib"
PUBLISH_TESTS_DIR="publish_tests"

echo "--- Cleaning up previous publish directories ---"
rm -rf "$SOLUTION_DIR/$PUBLISH_LIB_DIR"
rm -rf "$SOLUTION_DIR/$PUBLISH_TESTS_DIR"
mkdir -p "$SOLUTION_DIR/$PUBLISH_LIB_DIR"
mkdir -p "$SOLUTION_DIR/$PUBLISH_TESTS_DIR"

echo "--- Publishing WireGuardManager library ---"
dotnet publish "$SOLUTION_DIR/$LIBRARY_PROJECT_PATH" -c Release -o "$SOLUTION_DIR/$PUBLISH_LIB_DIR"

echo "--- Publishing WireGuardManager.IntegrationTests ---"
dotnet publish "$SOLUTION_DIR/$INTEGRATION_TEST_PROJECT_PATH" -c Release -o "$SOLUTION_DIR/$PUBLISH_TESTS_DIR"

echo "--- Building Docker image: $DOCKER_IMAGE_NAME ---"
# The Dockerfile needs to be in the context root (SOLUTION_DIR) or adjust path.
# For this script, assume Dockerfile is in $SOLUTION_DIR.
docker build -t $DOCKER_IMAGE_NAME -f "$SOLUTION_DIR/Dockerfile" "$SOLUTION_DIR"

echo "--- Running Integration Tests in Docker Container ---"
# Mount the published tests directory into /app/tests in the container.
# The Dockerfile's WORKDIR is /app.
# The .NET SDK image's default entrypoint is dotnet, so we can directly pass test command.
# Grant NET_ADMIN and SYS_MODULE capabilities for WireGuard interface operations.
# Use --rm to automatically remove the container when it exits by default.
DOCKER_RUN_OPTS="--rm"
CONTAINER_NAME="wgmanager-it-container-$(date +%s)" # Unique name if kept

if [[ "$1" == "--keep-container" ]]; then
    echo "--- Will keep container alive after test run ---"
    DOCKER_RUN_OPTS="--name $CONTAINER_NAME" # -d for detached is not used as we want to see test output
fi

docker run $DOCKER_RUN_OPTS \
    --cap-add NET_ADMIN \
    --cap-add SYS_MODULE \
    -v "$SOLUTION_DIR/$PUBLISH_TESTS_DIR:/app/tests" \
    $DOCKER_IMAGE_NAME \
    dotnet test "/app/tests/WireGuardManager.IntegrationTests.dll" --logger "console;verbosity=detailed"

if [[ "$1" == "--keep-container" ]]; then
    echo "--- Integration tests finished. Container '$CONTAINER_NAME' is kept. ---"
    echo "To access it: docker exec -it $CONTAINER_NAME bash"
else
    echo "--- Integration tests finished. Container was removed. ---"
fi
