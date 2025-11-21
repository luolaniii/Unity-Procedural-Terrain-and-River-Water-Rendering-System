# Vertex Picker Tool

一个强大的Unity编辑器工具，可以通过点击Scene视图来获取网格顶点的精确世界坐标。

## 🎯 功能特性

- **精确坐标获取**：支持精确交点和最近顶点两种模式
- **可视化反馈**：在Scene视图中显示选中的位置
- **多种输出格式**：坐标值、Vector3代码格式
- **独立工具**：完全独立的工具，可以轻松导出到其他项目

## 📁 文件结构

```
Vertex Picker/
├── Editor/
│   └── VertexPickerWindow.cs      # 主编辑器窗口
├── PixelToVertexTool.cs           # 核心工具类
├── Examples.cs                    # 使用示例
├── README.md                      # 使用说明
└── package.json                   # Unity包配置
```

## 🚀 快速开始

### 1. 导入工具
将整个`Vertex Picker`文件夹复制到你的Unity项目的`Assets`目录下。

### 2. 打开工具
- 在Unity菜单中找到：**Window → Vertex Picker**
- 或者在搜索框中输入"Vertex Picker"

### 3. 开始使用
1. ✅ 启用"Picking Mode Active"
2. 🖱️ 在Scene视图中**左键点击**任意位置
3. 📊 查看窗口底部的详细结果

## ⚙️ 设置选项

### 基本设置
- **Max Raycast Distance**: 射线检测的最大距离
- **Raycast Layer Mask**: 检测的层（-1表示所有层）

### 拾取模式
- **Use Exact Intersection**: 是否使用精确交点模式
  - ✅ **开启**：返回射线与表面的精确交点（推荐）
  - ❌ **关闭**：返回距离交点最近的顶点

### 可视化
- **Show Gizmos**: 是否显示可视化标记
- **Gizmo Size**: 可视化球体的大小

## 📊 结果信息

### 精确交点模式
- **Type**: Exact Intersection Point
- **World Position**: 精确的三维坐标
- **Object**: 被点击的物体名称

### 最近顶点模式
- **Vertex Index**: 顶点在网格中的索引
- **Distance to Hit**: 顶点到点击点的距离
- **World Position**: 顶点的世界坐标

## 🛠️ 实用功能

### 复制按钮
- **📋 Copy Position**: 复制坐标值 (x, y, z)
- **📝 Copy Vector3**: 复制Vector3代码格式
- **📊 Log Details**: 在Console中记录详细信息
- **🎯 Select Object**: 在Hierarchy中选择点击的对象

## 💻 编程使用

如果你想在代码中使用这个工具：

```csharp
using UnityEngine;

// 基本用法
var result = PixelToVertexTool.GetVertexWorldPosition(screenPoint);
if (result.success)
{
    Debug.Log($"Clicked at: {result.worldPosition}");
}

// 从射线检测结果获取
RaycastHit hit = // ... 执行射线检测
var result = PixelToVertexTool.GetNearestVertexFromHit(hit);
```

## 🎨 可视化

- **黄色球体**: 标记选中的位置
- **坐标标签**: 显示位置信息和坐标
- **Scene视图**: 实时更新显示

## 🔧 技术细节

- **兼容性**: Unity 2019.4+ (URP/HDRP/Built-in)
- **依赖**: 无额外依赖
- **性能**: 轻量级，实时处理
- **精度**: 支持亚像素级精度

## 📝 注意事项

1. **Layer设置**: 确保目标物体在正确的层上
2. **Collider**: 目标物体需要有MeshCollider才能被检测
3. **Scene视图**: 工具仅在Scene视图中工作

## 🐛 故障排除

### 无法检测到物体
- 检查物体的Layer设置
- 确保物体有MeshCollider组件
- 调整Raycast Layer Mask设置

### 坐标不准确
- 尝试开启"Use Exact Intersection"模式
- 检查Scene视图的缩放级别
- 确认没有其他物体阻挡射线

## 📄 许可证

这个工具是开源的，你可以自由使用、修改和分发。

## 🤝 贡献

欢迎提交问题和改进建议！

---

**享受使用Vertex Picker工具吧！** 🎉
