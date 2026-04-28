import { createHash } from "node:crypto";
import { GenericContainer, type StartedTestContainer } from "testcontainers";
import { S3Client, CreateBucketCommand } from "@aws-sdk/client-s3";

let container: StartedTestContainer;
let s3: S3Client;
const BUCKET_NAME = "files-integration-test";

/**
 * Adds Content-MD5 header middleware to an S3Client.
 *
 * AWS SDK v3 3.800+ switched from MD5 to CRC32 trailing checksums, but MinIO
 * still requires Content-MD5 for multi-object operations (DeleteObjects).
 * This middleware computes and attaches the header when it's missing.
 */
function addContentMd5Middleware(client: S3Client): void {
  client.middlewareStack.add(
    (next) => async (args) => {
      const request = args.request as { body?: unknown; headers: Record<string, string> };
      if (request.body && !request.headers["content-md5"]) {
        const body =
          typeof request.body === "string" ? Buffer.from(request.body) : (request.body as Buffer);
        if (Buffer.isBuffer(body)) {
          request.headers["content-md5"] = createHash("md5").update(body).digest("base64");
        }
      }
      return next(args);
    },
    { step: "build", name: "addContentMD5ForMinIO", priority: "low" },
  );
}

/**
 * Starts a MinIO container and creates the test bucket.
 */
export async function startMinIO(): Promise<void> {
  container = await new GenericContainer("minio/minio:latest")
    .withExposedPorts(9000)
    .withCommand(["server", "/data"])
    .withEnvironment({
      MINIO_ROOT_USER: "minioadmin",
      MINIO_ROOT_PASSWORD: "minioadmin",
    })
    .start();

  const host = container.getHost();
  const port = container.getMappedPort(9000);

  s3 = new S3Client({
    endpoint: `http://${host}:${port}`,
    region: "us-east-1",
    credentials: {
      accessKeyId: "minioadmin",
      secretAccessKey: "minioadmin",
    },
    forcePathStyle: true,
    requestChecksumCalculation: "WHEN_REQUIRED",
    responseChecksumValidation: "WHEN_REQUIRED",
  });

  addContentMd5Middleware(s3);

  await s3.send(new CreateBucketCommand({ Bucket: BUCKET_NAME }));
}

/**
 * Stops the MinIO container and destroys the S3 client.
 */
export async function stopMinIO(): Promise<void> {
  s3?.destroy();
  await container?.stop();
}

export function getS3Client(): S3Client {
  return s3;
}

export function getBucketName(): string {
  return BUCKET_NAME;
}
