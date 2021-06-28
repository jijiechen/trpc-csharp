
## 更新 tRPC Proto

目前 Grpc.Tools 工具中 `protoc.exe` 尚不支持从命令行指定最终生成的 CSharp 类的命名空间，因此最好的方式是在原有的 `trpc.proto` 文件中指明 CSharp 的命名空间。

```proto
option csharp_namespace = "TrpcSharp.Protocol.Standard";
```

详情请参考此 [Issue](https://github.com/protocolbuffers/protobuf/issues/6846)；此外，protoc.exe 的参数 `--csharp_opt=base_namespace=Example` 并不是用于指令命名空间的，而是用于生成目录结构的，[参考资料](https://developers.google.com/protocol-buffers/docs/reference/csharp-generated)

