import { Handle, Position, type NodeProps } from "@xyflow/react";

export type OrgUnitNodeData = {
  label: string;
  unitType: string;
  orgUnitId: string;
  onAssignMember?: (memberId: string) => void | Promise<boolean>;
};

export function OrgUnitNode({ data, selected }: NodeProps) {
  const d = data as OrgUnitNodeData;
  const bg =
    d.unitType === "Department" ? "#bfdbfe" : d.unitType === "Team" ? "#bbf7d0" : "#e5e7eb";
  return (
    <div
      style={{
        padding: 10,
        borderRadius: 8,
        border: selected ? "2px solid #2563eb" : "1px solid #64748b",
        background: bg,
        minWidth: 130,
      }}
      onDragOver={(e) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = "move";
      }}
      onDrop={(e) => {
        e.preventDefault();
        const id = e.dataTransfer.getData("text/plain");
        if (id && d.onAssignMember) {
          void Promise.resolve(d.onAssignMember(id));
        }
      }}
    >
      <Handle type="target" position={Position.Top} />
      <div style={{ fontWeight: 600 }}>{d.label}</div>
      <div style={{ fontSize: 12, opacity: 0.85 }}>{d.unitType}</div>
      <Handle type="source" position={Position.Bottom} />
    </div>
  );
}
