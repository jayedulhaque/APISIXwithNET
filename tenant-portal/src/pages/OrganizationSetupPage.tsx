import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  Background,
  Controls,
  ReactFlow,
  addEdge,
  useEdgesState,
  useNodesState,
  type Connection,
  type Edge,
  type Node,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import { userManager } from "../auth/oidc";
import { apiJson } from "../api/client";
import { useAuthStore } from "../store/authStore";
import { useUiStore } from "../store/uiStore";
import { OrgUnitNode } from "../components/OrgUnitNode";
import type {
  AssignedMember,
  OrgTreeResponse,
  OrgUnitTreeNode,
  RoutingPreviewResponse,
  ServiceConfigListItem,
  ServiceNodeTree,
  UnassignedMember,
} from "../types";

const nodeTypes = { orgUnit: OrgUnitNode };

type FlatServiceNode = { id: string; name: string; nodeType: string; parentId: string | null };

function flattenServiceNodes(nodes: ServiceNodeTree[]): FlatServiceNode[] {
  const out: FlatServiceNode[] = [];
  const walk = (n: ServiceNodeTree, parentId: string | null) => {
    out.push({ id: n.id, name: n.name, nodeType: n.nodeType, parentId });
    for (const c of n.children) {
      walk(c, n.id);
    }
  };
  for (const n of nodes) {
    walk(n, null);
  }
  return out;
}

function flattenOrgTree(roots: OrgUnitTreeNode[]): Map<string, { name: string; unitType: string }> {
  const map = new Map<string, { name: string; unitType: string }>();
  const walk = (n: OrgUnitTreeNode) => {
    map.set(n.id, { name: n.name, unitType: n.unitType });
    for (const c of n.children) {
      walk(c);
    }
  };
  for (const r of roots) {
    walk(r);
  }
  return map;
}

function inferChildServiceNodeType(flat: FlatServiceNode[], parentId: string): "Type" | "Category" | "SubCategory" | null {
  if (parentId === "") {
    return "Type";
  }
  const parent = flat.find((x) => x.id === parentId);
  if (!parent) {
    return null;
  }
  if (parent.nodeType === "Type") {
    return "Category";
  }
  if (parent.nodeType === "Category") {
    return "SubCategory";
  }
  return null;
}

function buildFlowFromTree(
  roots: OrgUnitTreeNode[],
  onAssign: (orgUnitId: string, memberId: string) => Promise<boolean>,
): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = [];
  const edges: Edge[] = [];
  let gx = 0;

  function dfs(n: OrgUnitTreeNode, depth: number, parentFlowId: string | null) {
    const flowId = `ou-${n.id}`;
    nodes.push({
      id: flowId,
      type: "orgUnit",
      position: { x: gx++ * 240, y: depth * 140 },
      data: {
        label: n.name,
        unitType: n.unitType,
        orgUnitId: n.id,
        onAssignMember: (mid: string) => onAssign(n.id, mid),
      },
    });
    if (parentFlowId) {
      edges.push({
        id: `e-${parentFlowId}-${flowId}`,
        source: parentFlowId,
        target: flowId,
        type: "smoothstep",
      });
    }
    for (const c of n.children) {
      dfs(c, depth + 1, flowId);
    }
  }

  for (const r of roots) {
    dfs(r, 0, null);
  }
  return { nodes, edges };
}

export function OrganizationSetupPage() {
  const accessToken = useAuthStore((s) => s.accessToken);
  const setAccessToken = useAuthStore((s) => s.setAccessToken);
  const [searchParams, setSearchParams] = useSearchParams();
  const selectedOrgUnitId = useUiStore((s) => s.selectedOrgUnitId);
  const selectedMemberId = useUiStore((s) => s.selectedMemberId);
  const setOrgUnit = useUiStore((s) => s.setOrgUnit);
  const setMember = useUiStore((s) => s.setMember);

  const [unassigned, setUnassigned] = useState<UnassignedMember[]>([]);
  const [assigned, setAssigned] = useState<AssignedMember[]>([]);
  const [orgRoots, setOrgRoots] = useState<OrgUnitTreeNode[]>([]);
  const [serviceRoots, setServiceRoots] = useState<ServiceNodeTree[]>([]);
  const [nodes, setNodes, onNodesChange] = useNodesState<Node>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<Edge>([]);
  const [error, setError] = useState<string | null>(null);
  const [ctxMenu, setCtxMenu] = useState<{ x: number; y: number; orgUnitId: string } | null>(null);

  const [metaEntries, setMetaEntries] = useState<{ metaKey: string; metaValue: string }[]>([]);
  const [svcNodeId, setSvcNodeId] = useState("");
  const [sla, setSla] = useState(24);
  const [prio, setPrio] = useState(1);

  const [orgHasRoots, setOrgHasRoots] = useState(false);
  const [orgTreeLoaded, setOrgTreeLoaded] = useState(false);
  const [serviceConfigList, setServiceConfigList] = useState<ServiceConfigListItem[]>([]);
  const [newSvcName, setNewSvcName] = useState("");
  const [newSvcParentId, setNewSvcParentId] = useState("");
  const [previewSvcId, setPreviewSvcId] = useState("");
  const [previewResult, setPreviewResult] = useState<RoutingPreviewResponse | null>(null);
  const [previewBusy, setPreviewBusy] = useState(false);

  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteToken, setInviteToken] = useState<string | null>(null);
  const [inviteInfo, setInviteInfo] = useState<{ tenantName: string; email: string } | null>(null);

  const loadTreeRef = useRef<() => Promise<void>>(async () => undefined);

  const reloadMemberLists = useCallback(async () => {
    const [u, a] = await Promise.all([
      apiJson<UnassignedMember[]>("/api/members/unassigned"),
      apiJson<AssignedMember[]>("/api/members/assigned"),
    ]);
    setUnassigned(u);
    setAssigned(a);
  }, []);

  const orgUnitMap = useMemo(() => flattenOrgTree(orgRoots), [orgRoots]);

  const loadTree = useCallback(async () => {
    const res = await apiJson<OrgTreeResponse>("/api/org-units/tree");
    setOrgRoots(res.nodes);
    setOrgHasRoots(res.nodes.length > 0);
    setOrgTreeLoaded(true);
    const onAssign = async (orgUnitId: string, memberId: string): Promise<boolean> => {
      try {
        setError(null);
        const raw = window.prompt("Designation (e.g. Agent, Lead)", "Agent");
        if (raw === null) {
          return false;
        }
        const designation = raw.trim() || "Agent";
        await apiJson("/api/assignments", {
          method: "POST",
          body: JSON.stringify({ memberId, orgUnitId, designation }),
        });
        await reloadMemberLists();
        await loadTreeRef.current();
        return true;
      } catch (e) {
        setError(e instanceof Error ? e.message : "Assignment failed");
        return false;
      }
    };
    const built = buildFlowFromTree(res.nodes, onAssign);
    setNodes(built.nodes);
    setEdges(built.edges);
  }, [reloadMemberLists, setNodes, setEdges]);

  loadTreeRef.current = loadTree;

  const refreshServices = useCallback(async () => {
    const res = await apiJson<{ nodes: ServiceNodeTree[] }>("/api/service-nodes/tree");
    setServiceRoots(res.nodes);
  }, []);

  const reloadServiceConfigs = useCallback(async () => {
    const res = await apiJson<{ items: ServiceConfigListItem[] }>("/api/service-configs");
    setServiceConfigList(res.items);
  }, []);

  useEffect(() => {
    userManager.getUser().then((u) => {
      if (u?.access_token) {
        setAccessToken(u.access_token);
      }
    });
  }, [setAccessToken]);

  useEffect(() => {
    if (!accessToken) {
      return;
    }
    void loadTree().catch((e) => setError(String(e)));
    void refreshServices().catch(() => undefined);
    void reloadServiceConfigs().catch(() => undefined);
  }, [accessToken, loadTree, refreshServices, reloadServiceConfigs]);

  useEffect(() => {
    if (!accessToken) {
      return;
    }
    void reloadMemberLists().catch(() => undefined);
    const id = window.setInterval(() => {
      void reloadMemberLists().catch(() => undefined);
    }, 5000);
    return () => window.clearInterval(id);
  }, [accessToken, reloadMemberLists]);

  const renameOrgUnit = useCallback(
    async (orgUnitId: string) => {
      const info = orgUnitMap.get(orgUnitId);
      if (!info) {
        return;
      }
      const next = window.prompt("Org unit name", info.name);
      if (next === null) {
        return;
      }
      const trimmed = next.trim();
      if (!trimmed) {
        return;
      }
      try {
        setError(null);
        await apiJson(`/api/org-units/${orgUnitId}`, {
          method: "PUT",
          body: JSON.stringify({ name: trimmed, unitType: info.unitType }),
        });
        setCtxMenu(null);
        await loadTree();
      } catch (e) {
        setError(e instanceof Error ? e.message : "Rename failed");
      }
    },
    [orgUnitMap, loadTree],
  );

  const deleteOrgUnit = useCallback(
    async (orgUnitId: string) => {
      const ok = window.confirm(
        "Delete this org unit? Remove child units and service mappings to this unit first. Member assignments to this unit will be removed.",
      );
      if (!ok) {
        return;
      }
      try {
        setError(null);
        await apiJson<void>(`/api/org-units/${orgUnitId}`, { method: "DELETE" });
        setCtxMenu(null);
        if (selectedOrgUnitId === orgUnitId) {
          setOrgUnit(null);
        }
        await loadTree();
        await reloadServiceConfigs();
      } catch (e) {
        setError(e instanceof Error ? e.message : "Delete failed");
      }
    },
    [loadTree, reloadServiceConfigs, selectedOrgUnitId, setOrgUnit],
  );

  useEffect(() => {
    const fromQuery = searchParams.get("inviteToken");
    const fromSession = sessionStorage.getItem("pending_invite_token");
    const token = fromQuery ?? fromSession;
    if (!token || !accessToken) {
      return;
    }
    setInviteToken(token);
    void apiJson<{
      valid?: boolean;
      Valid?: boolean;
      tenantName?: string;
      TenantName?: string;
      email?: string;
      Email?: string;
    }>(`/api/invitations/validate?token=${encodeURIComponent(token)}`)
      .then((v) => {
        const ok = v.valid ?? v.Valid;
        const name = v.tenantName ?? v.TenantName;
        const em = v.email ?? v.Email;
        if (ok && name && em) {
          setInviteInfo({ tenantName: name, email: em });
          setInviteOpen(true);
        }
      })
      .catch(() => undefined);
  }, [accessToken, searchParams]);

  useEffect(() => {
    if (!selectedMemberId || !accessToken) {
      return;
    }
    void apiJson<{ entries: { metaKey: string; metaValue: string }[] }>(`/api/members/${selectedMemberId}/meta`)
      .then((r) => setMetaEntries(r.entries))
      .catch(() => setMetaEntries([]));
  }, [selectedMemberId, accessToken]);

  const flatServices = useMemo(() => flattenServiceNodes(serviceRoots), [serviceRoots]);
  const subCategoryServices = useMemo(
    () => flatServices.filter((s) => s.nodeType === "SubCategory"),
    [flatServices],
  );

  async function saveMeta() {
    if (!selectedMemberId) {
      return;
    }
    await apiJson(`/api/members/${selectedMemberId}/meta`, {
      method: "PUT",
      body: JSON.stringify({ entries: metaEntries }),
    });
    setError(null);
  }

  async function saveServiceConfig() {
    if (!selectedOrgUnitId || !svcNodeId) {
      return;
    }
    await apiJson("/api/service-configs", {
      method: "POST",
      body: JSON.stringify({
        serviceNodeId: svcNodeId,
        assignedOrgUnitId: selectedOrgUnitId,
        slaHours: sla,
        priority: prio,
      }),
    });
    setError(null);
    await reloadServiceConfigs();
  }

  async function acceptInvite() {
    if (!inviteToken) {
      return;
    }
    await apiJson("/api/invitations/accept", {
      method: "POST",
      body: JSON.stringify({ token: inviteToken }),
    });
    sessionStorage.removeItem("pending_invite_token");
    setInviteOpen(false);
    setSearchParams({});
    window.location.reload();
  }

  async function createChild(parentId: string | null, unitType: string) {
    const name = window.prompt(`Name for new ${unitType}`);
    if (!name?.trim()) {
      return;
    }
    await apiJson("/api/org-units", {
      method: "POST",
      body: JSON.stringify({
        name: name.trim(),
        unitType,
        parentId: parentId,
      }),
    });
    setCtxMenu(null);
    await loadTree();
  }

  async function createRootDepartment() {
    const name = window.prompt("Name for the root department");
    if (!name?.trim()) {
      return;
    }
    try {
      setError(null);
      await apiJson("/api/org-units", {
        method: "POST",
        body: JSON.stringify({
          name: name.trim(),
          unitType: "Department",
          parentId: null,
        }),
      });
      await loadTree();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to create department");
    }
  }

  async function createServiceNode() {
    const name = newSvcName.trim();
    if (!name) {
      setError("Enter a service name.");
      return;
    }
    const nodeType = inferChildServiceNodeType(flatServices, newSvcParentId);
    if (!nodeType) {
      setError("Choose a valid parent: root for a Type, or a Type or Category node.");
      return;
    }
    const parentId = newSvcParentId === "" ? null : newSvcParentId;
    try {
      setError(null);
      await apiJson("/api/service-nodes", {
        method: "POST",
        body: JSON.stringify({ name, nodeType, parentId }),
      });
      setNewSvcName("");
      await refreshServices();
      await reloadServiceConfigs();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to create service node");
    }
  }

  async function runRoutingPreview() {
    if (!previewSvcId) {
      return;
    }
    setPreviewBusy(true);
    setPreviewResult(null);
    try {
      setError(null);
      const r = await apiJson<RoutingPreviewResponse>(
        `/api/routing/preview?serviceNodeId=${encodeURIComponent(previewSvcId)}`,
      );
      setPreviewResult(r);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Routing preview failed");
    } finally {
      setPreviewBusy(false);
    }
  }

  const onConnect = useCallback(
    (params: Connection) => setEdges((eds) => addEdge(params, eds)),
    [setEdges],
  );

  if (!accessToken) {
    return (
      <div style={{ padding: 24 }}>
        <p>Sign in to manage the organization.</p>
        <button type="button" onClick={() => userManager.signinRedirect()}>
          Login
        </button>
        <p>
          <Link to="/">Home</Link>
        </p>
      </div>
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100vh" }}>
      <header
        style={{
          padding: "8px 16px",
          borderBottom: "1px solid #e2e8f0",
          display: "flex",
          alignItems: "center",
          gap: 16,
          background: "white",
        }}
      >
        <strong>Organization setup</strong>
        <button type="button" onClick={() => void createRootDepartment()}>
          Create root department
        </button>
        <Link to="/dashboard">Dashboard</Link>
        <button
          type="button"
          onClick={() => {
            void userManager.signoutRedirect();
            setAccessToken(null);
          }}
        >
          Logout
        </button>
      </header>

      {inviteOpen && inviteInfo && (
        <div
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(0,0,0,0.4)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 50,
          }}
        >
          <div style={{ background: "white", padding: 24, borderRadius: 8, maxWidth: 400 }}>
            <h2>Accept invitation</h2>
            <p>
              Join <strong>{inviteInfo.tenantName}</strong> as <strong>{inviteInfo.email}</strong>?
            </p>
            <div style={{ display: "flex", gap: 8, marginTop: 16 }}>
              <button type="button" onClick={() => void acceptInvite()}>
                Accept
              </button>
              <button
                type="button"
                onClick={() => {
                  setInviteOpen(false);
                  sessionStorage.removeItem("pending_invite_token");
                }}
              >
                Later
              </button>
            </div>
          </div>
        </div>
      )}

      <div style={{ display: "flex", flex: 1, minHeight: 0 }}>
        <aside
          style={{
            width: 288,
            borderRight: "1px solid #e2e8f0",
            padding: 12,
            overflow: "auto",
            background: "#f8fafc",
          }}
        >
          <h3 style={{ marginTop: 0 }}>Unassigned members</h3>
          <p style={{ fontSize: 12, color: "#64748b" }}>
            Refreshes every 5s. Select a member, then click an org unit (or drag onto a unit).
          </p>
          {unassigned.length === 0 && <p style={{ color: "#64748b" }}>None</p>}
          <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
            {unassigned.map((m) => {
              const mid = m.memberId;
              return (
                <li
                  key={mid}
                  draggable
                  onDragStart={(e) => {
                    e.dataTransfer.setData("text/plain", mid);
                    e.dataTransfer.effectAllowed = "move";
                  }}
                  onClick={() => setMember(mid)}
                  style={{
                    padding: 8,
                    marginBottom: 6,
                    background: selectedMemberId === mid ? "#dbeafe" : "white",
                    borderRadius: 6,
                    cursor: "grab",
                    border: "1px solid #e2e8f0",
                  }}
                >
                  <div style={{ fontSize: 12 }}>{m.email}</div>
                  <div style={{ fontSize: 11, color: "#64748b" }}>{m.status}</div>
                </li>
              );
            })}
          </ul>

          <h3 style={{ marginTop: 20 }}>Assigned members</h3>
          <p style={{ fontSize: 12, color: "#64748b" }}>
            Click a member to edit Expertise / Skills (meta). Refreshes with unassigned.
          </p>
          {assigned.length === 0 && <p style={{ color: "#64748b" }}>None</p>}
          <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
            {assigned.map((m) => {
              const mid = m.memberId;
              const summary = m.assignments.map((a) => `${a.orgUnitName} (${a.designation})`).join(", ");
              return (
                <li
                  key={mid}
                  onClick={() => setMember(mid)}
                  style={{
                    padding: 8,
                    marginBottom: 6,
                    background: selectedMemberId === mid ? "#dbeafe" : "white",
                    borderRadius: 6,
                    cursor: "pointer",
                    border: "1px solid #e2e8f0",
                  }}
                >
                  <div style={{ fontSize: 12 }}>{m.email}</div>
                  <div style={{ fontSize: 11, color: "#64748b" }}>{m.status}</div>
                  <div style={{ fontSize: 10, color: "#94a3b8", marginTop: 4 }}>{summary}</div>
                </li>
              );
            })}
          </ul>

          <h3 style={{ marginTop: 20 }}>Service catalog</h3>
          <p style={{ fontSize: 12, color: "#64748b" }}>Add Type → Category → SubCategory.</p>
          <label style={{ display: "block", fontSize: 12, marginBottom: 6 }}>
            Parent
            <select
              style={{ display: "block", width: "100%", marginTop: 4 }}
              value={newSvcParentId}
              onChange={(e) => setNewSvcParentId(e.target.value)}
            >
              <option value="">New root Type</option>
              {flatServices
                .filter((s) => s.nodeType === "Type" || s.nodeType === "Category")
                .map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.nodeType}: {s.name}
                  </option>
                ))}
            </select>
          </label>
          <label style={{ display: "block", fontSize: 12, marginBottom: 6 }}>
            Name
            <input
              style={{ display: "block", width: "100%", marginTop: 4 }}
              value={newSvcName}
              onChange={(e) => setNewSvcName(e.target.value)}
              placeholder="e.g. Hardware Support"
            />
          </label>
          <button type="button" onClick={() => void createServiceNode()}>
            Add service node
          </button>
        </aside>

        <main style={{ flex: 1, position: "relative" }}>
          {error && (
            <div style={{ padding: 8, background: "#fee2e2", color: "#991b1b", fontSize: 13 }}>{error}</div>
          )}
          {orgTreeLoaded && !orgHasRoots && (
            <div
              style={{
                position: "absolute",
                inset: 0,
                zIndex: 5,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                background: "rgba(248,250,252,0.92)",
                flexDirection: "column",
                gap: 12,
                pointerEvents: "none",
              }}
            >
              <p style={{ margin: 0, color: "#334155", fontSize: 15 }}>No organization units yet.</p>
              <button
                type="button"
                style={{ pointerEvents: "auto" }}
                onClick={() => void createRootDepartment()}
              >
                Create root department
              </button>
            </div>
          )}
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            nodeTypes={nodeTypes}
            fitView
            onDragOver={(e) => {
              e.preventDefault();
              e.dataTransfer.dropEffect = "move";
            }}
            onNodeClick={(_, n) => {
              const data = n.data as {
                orgUnitId?: string;
                onAssignMember?: (memberId: string) => Promise<boolean>;
              };
              const orgId = data.orgUnitId;
              if (!orgId) {
                return;
              }
              void (async () => {
                if (selectedMemberId && data.onAssignMember) {
                  const ok = await data.onAssignMember(selectedMemberId);
                  if (ok) {
                    setMember(null);
                    setOrgUnit(orgId);
                  }
                  return;
                }
                setOrgUnit(orgId);
              })();
            }}
            onNodeContextMenu={(e, node) => {
              e.preventDefault();
              const id = (node.data as { orgUnitId?: string }).orgUnitId;
              if (id) {
                setCtxMenu({ x: e.clientX, y: e.clientY, orgUnitId: id });
              }
            }}
            onPaneClick={() => {
              setCtxMenu(null);
            }}
          >
            <Background />
            <Controls />
          </ReactFlow>

          {ctxMenu && (
            <div
              style={{
                position: "fixed",
                left: ctxMenu.x,
                top: ctxMenu.y,
                zIndex: 40,
                background: "white",
                border: "1px solid #cbd5e1",
                borderRadius: 6,
                boxShadow: "0 4px 12px rgba(0,0,0,0.15)",
                minWidth: 180,
              }}
            >
              <button
                type="button"
                style={{ display: "block", width: "100%", padding: 8, border: "none", background: "none", textAlign: "left" }}
                onClick={() => void createChild(ctxMenu.orgUnitId, "Department")}
              >
                Add Sub-Department
              </button>
              <button
                type="button"
                style={{ display: "block", width: "100%", padding: 8, border: "none", background: "none", textAlign: "left" }}
                onClick={() => void createChild(ctxMenu.orgUnitId, "Team")}
              >
                Add Team
              </button>
              <div style={{ borderTop: "1px solid #e2e8f0", margin: "4px 0" }} />
              <button
                type="button"
                style={{ display: "block", width: "100%", padding: 8, border: "none", background: "none", textAlign: "left" }}
                onClick={() => void renameOrgUnit(ctxMenu.orgUnitId)}
              >
                Rename…
              </button>
              <button
                type="button"
                style={{ display: "block", width: "100%", padding: 8, border: "none", background: "none", textAlign: "left", color: "#991b1b" }}
                onClick={() => void deleteOrgUnit(ctxMenu.orgUnitId)}
              >
                Delete…
              </button>
            </div>
          )}
        </main>

        <aside
          style={{
            width: 380,
            borderLeft: "1px solid #e2e8f0",
            padding: 12,
            overflow: "auto",
            background: "white",
          }}
        >
          <h3 style={{ marginTop: 0 }}>Inspector</h3>
          {selectedMemberId && (
            <div>
              <h4>Member meta</h4>
              <p style={{ fontSize: 12, color: "#64748b" }}>
                Use keys <strong>Expertise</strong> or <strong>Skills</strong>; value should match a SubCategory name for routing preview.
              </p>
              <p style={{ fontSize: 12 }}>Member id: {selectedMemberId}</p>
              {metaEntries.map((row, i) => (
                <div key={i} style={{ display: "flex", gap: 4, marginBottom: 6 }}>
                  <input
                    placeholder="key"
                    value={row.metaKey}
                    onChange={(e) => {
                      const next = [...metaEntries];
                      next[i] = { ...next[i], metaKey: e.target.value };
                      setMetaEntries(next);
                    }}
                    style={{ flex: 1 }}
                  />
                  <input
                    placeholder="value"
                    value={row.metaValue}
                    onChange={(e) => {
                      const next = [...metaEntries];
                      next[i] = { ...next[i], metaValue: e.target.value };
                      setMetaEntries(next);
                    }}
                    style={{ flex: 1 }}
                  />
                </div>
              ))}
              <button type="button" onClick={() => setMetaEntries([...metaEntries, { metaKey: "", metaValue: "" }])}>
                + Row
              </button>
              <button type="button" style={{ marginLeft: 8 }} onClick={() => void saveMeta()}>
                Save meta
              </button>
            </div>
          )}

          {selectedOrgUnitId && (
            <div style={{ marginTop: selectedMemberId ? 24 : 0 }}>
              <h4>Org unit</h4>
              <p style={{ fontSize: 13, fontWeight: 600 }}>
                {orgUnitMap.get(selectedOrgUnitId)?.name ?? selectedOrgUnitId}
              </p>
              <p style={{ fontSize: 12, color: "#64748b" }}>
                Type: {orgUnitMap.get(selectedOrgUnitId)?.unitType ?? "—"}
              </p>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 8, marginBottom: 16 }}>
                <button type="button" onClick={() => void renameOrgUnit(selectedOrgUnitId)}>
                  Rename
                </button>
                <button type="button" onClick={() => void deleteOrgUnit(selectedOrgUnitId)}>
                  Delete
                </button>
              </div>

              <h4>Service mapping</h4>
              <p style={{ fontSize: 12, color: "#64748b" }}>Map a SubCategory (e.g. Laptop Repair) to this team.</p>
              <label style={{ display: "block", marginBottom: 8 }}>
                SubCategory
                <select
                  style={{ display: "block", width: "100%", marginTop: 4 }}
                  value={svcNodeId}
                  onChange={(e) => setSvcNodeId(e.target.value)}
                >
                  <option value="">Select…</option>
                  {subCategoryServices.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
              </label>
              <label style={{ display: "block", marginBottom: 8 }}>
                SLA hours
                <input
                  type="number"
                  style={{ display: "block", width: "100%" }}
                  value={sla}
                  onChange={(e) => setSla(Number(e.target.value))}
                />
              </label>
              <label style={{ display: "block", marginBottom: 8 }}>
                Priority
                <input
                  type="number"
                  style={{ display: "block", width: "100%" }}
                  value={prio}
                  onChange={(e) => setPrio(Number(e.target.value))}
                />
              </label>
              <button type="button" onClick={() => void saveServiceConfig()}>
                Save mapping
              </button>
              <h4 style={{ marginTop: 20 }}>Configured mappings</h4>
              {serviceConfigList.length === 0 ? (
                <p style={{ fontSize: 12, color: "#64748b" }}>None yet.</p>
              ) : (
                <ul style={{ fontSize: 11, paddingLeft: 16, margin: "8px 0 0 0" }}>
                  {serviceConfigList.map((c) => (
                    <li key={c.id} style={{ marginBottom: 6 }}>
                      <strong>{c.serviceName}</strong> → {c.orgUnitName}{" "}
                      <span style={{ color: "#64748b" }}>
                        (SLA {c.slaHours}h, P{c.priority})
                      </span>
                    </li>
                  ))}
                </ul>
              )}
              <p style={{ fontSize: 12, marginTop: 16 }}>
                <button type="button" onClick={() => void createChild(selectedOrgUnitId, "Department")}>
                  Add child (Department)
                </button>
              </p>
              <p style={{ fontSize: 12 }}>
                <button type="button" onClick={() => void createChild(selectedOrgUnitId, "Team")}>
                  Add child (Team)
                </button>
              </p>
            </div>
          )}

          <div style={{ marginTop: selectedOrgUnitId || selectedMemberId ? 24 : 0 }}>
            <h4>Ticket routing preview</h4>
            <p style={{ fontSize: 12, color: "#64748b" }}>
              Pick a SubCategory with a saved mapping. Agents qualify if Expertise/Skills matches the service name.
            </p>
            <label style={{ display: "block", marginBottom: 8 }}>
              SubCategory
              <select
                style={{ display: "block", width: "100%", marginTop: 4 }}
                value={previewSvcId}
                onChange={(e) => setPreviewSvcId(e.target.value)}
              >
                <option value="">Select…</option>
                {subCategoryServices.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </label>
            <button type="button" disabled={previewBusy || !previewSvcId} onClick={() => void runRoutingPreview()}>
              {previewBusy ? "Loading…" : "Run preview"}
            </button>
            {previewResult && (
              <div style={{ marginTop: 12, fontSize: 12 }}>
                {previewResult.message && <p style={{ color: "#64748b" }}>{previewResult.message}</p>}
                {previewResult.team && (
                  <p>
                    <strong>Team:</strong> {previewResult.team.name} ({previewResult.team.unitType})
                  </p>
                )}
                {previewResult.slaHours != null && (
                  <p style={{ color: "#64748b" }}>
                    SLA {previewResult.slaHours}h · Priority {previewResult.priority}
                  </p>
                )}
                {previewResult.agents && previewResult.agents.length > 0 && (
                  <ul style={{ paddingLeft: 16, margin: "8px 0 0 0" }}>
                    {previewResult.agents.map((a) => (
                      <li key={a.memberId} style={{ marginBottom: 6 }}>
                        {a.email}{" "}
                        <span style={{ color: a.qualified ? "#15803d" : "#64748b" }}>
                          ({a.qualified ? "qualified" : "not qualified"})
                        </span>
                        {a.note && <div style={{ fontSize: 11, color: "#64748b" }}>{a.note}</div>}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )}
          </div>

          {!selectedMemberId && !selectedOrgUnitId && (
            <p style={{ color: "#64748b" }}>Select a member or org unit.</p>
          )}
        </aside>
      </div>
    </div>
  );
}
