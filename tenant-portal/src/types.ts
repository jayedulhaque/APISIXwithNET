export type TenantCurrentResponse = {
  tenantId: string;
  name: string;
  domain: string;
};

export type MeResponse =
  | {
      onboarded: false;
      casdoorUid: string;
      email: string;
    }
  | {
      onboarded: true;
      casdoorUid: string;
      tenantId: string;
      /** Present from GET /api/me — use for company setup without calling GET /api/tenants/current */
      tenant: { name: string; domain: string };
      member: {
        memberId: string;
        tenantId: string;
        email: string;
        status: string;
      };
    };

export type OrgUnitTreeNode = {
  id: string;
  name: string;
  unitType: string;
  children: OrgUnitTreeNode[];
};

export type OrgTreeResponse = { nodes: OrgUnitTreeNode[] };

export type UnassignedMember = {
  memberId: string;
  email: string;
  status: string;
};

export type AssignedMemberAssignment = {
  orgUnitId: string;
  orgUnitName: string;
  designation: string;
};

export type AssignedMember = {
  memberId: string;
  email: string;
  status: string;
  assignments: AssignedMemberAssignment[];
};

export type ServiceNodeTree = {
  id: string;
  name: string;
  nodeType: string;
  children: ServiceNodeTree[];
};

export type ServiceTreeResponse = { nodes: ServiceNodeTree[] };

export type ServiceConfigListItem = {
  id: string;
  serviceNodeId: string;
  serviceName: string;
  serviceNodeType: string;
  assignedOrgUnitId: string;
  orgUnitName: string;
  orgUnitType: string;
  slaHours: number;
  priority: number;
};

export type RoutingPreviewResponse = {
  serviceNodeId: string;
  serviceName: string;
  nodeType: string;
  team: { id: string; name: string; unitType: string } | null;
  slaHours?: number;
  priority?: number;
  agents?: Array<{
    memberId: string;
    email: string;
    qualified: boolean;
    meta: { metaKey: string; metaValue: string }[];
    note?: string;
  }>;
  message?: string;
};
